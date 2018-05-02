using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Microsoft.Synchronization;
using Microsoft.Synchronization.Data;
using Microsoft.Synchronization.Data.SqlServer;
using Newtonsoft.Json;

namespace EjemploDeManejoSyncFramework
{
    public class ManagerSyncFramework
    {
        private static readonly object _lockAprovisionamiento = new object();
        private static readonly object _lockReplication = new object();

        private string _esquemaMetadataSyncFramework;
        private string _esquemaQueSeReplica;
        private int _hilosAprovisionando = 0;
        private string _nombreDeBaseRemota;
        private string _prefijoMetadataSyncFramework;
        private string _prefijoParaNombreDeAmbito;
        private Semaphore _semaphore;
        private bool _suscribirATodosLosEventos;
        private Semaphore _replicationSemaphore;

        private List<Thread> _hilosEnEjecucion = new List<Thread>();
        private List<Thread> HilosEnEjecucion
        {
            get
            {
                lock (_lockReplication)
                {
                    return this._hilosEnEjecucion;
                }
            }
        }
        public delegate void LoguearEventHandler(string renglon);

        public delegate void replicaFinalizada();

        public event LoguearEventHandler OnLoguearVisualmente;

        public event replicaFinalizada OnProcesoFinalizado;

        public string TiempoTotalTranscurrido { get; set; }

        public void IniciarReplica(ParametrosReplica parametrosReplica)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            //Inicializa las propiedades de esquemas y prefijos en la clase.
            InicializarVariablesDeSeteoSync(parametrosReplica);

            //Si se tiene o no configurado el tracing en el app.config, se va a loguear.
            LoguearNivelDeTracingConfigurador();

            //Conecta con los endpoints
            var conexionLocalSql = GetSQLConnection(parametrosReplica.StringConnectionLocal);
            var conexionRemotoSql = GetSQLConnection(parametrosReplica.StringConnectionRemoto);

            //Crea los esquemas segun corresponda.
            ControlarEsquemas(conexionLocalSql, parametrosReplica.esquemaQueSeReplica);
            ControlarEsquemas(conexionRemotoSql, parametrosReplica.esquemaQueSeReplica);

            //Limpia toda la metadata relacionada a Sync Framework de ambos endpoints segun corresponda.
            if (parametrosReplica.LimpiarServidorLocal) DesaprovicionarServidor(conexionLocalSql);
            if (parametrosReplica.LimpiarServidorRemoto) DesaprovicionarServidor(conexionRemotoSql);

            //Obtiene los ambitos seleccionados.
            List<DbSyncScopeDescription> Ambitos = ObtenerAmbitosAProcesar(parametrosReplica);

            //Elimina y crea los ambitos segun corresponda con lo deseado.
            MantenerAprovisionamientoDeAmbitosEnHilos(parametrosReplica, _esquemaMetadataSyncFramework, _prefijoMetadataSyncFramework, Ambitos);
            _nombreDeBaseRemota = conexionRemotoSql.Database;

            //Crea si corresponde la tabla de Anclas.
            MantenerTablaDeAnclasLocal(conexionLocalSql);

            //Inicia proceso de replica
            if (parametrosReplica.RealizarReplica)
            {
                ReplicarAmbitos(Ambitos, parametrosReplica);
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            TiempoTotalTranscurrido = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);

            if (OnProcesoFinalizado != null)
                OnProcesoFinalizado();
        }

        public string ObtenerAmbitosSerializados(ParametrosReplica parametros)
        {
            InicializarVariablesDeSeteoSync(parametros);
            //ConectarConSQLServer(parametros.StringConnectionLocal, parametros.StringConnectionRemoto);

            var ambitos = ObtenerAmbitosAProcesar(parametros);

            return JsonConvert.SerializeObject(ambitos);
        }

        [DebuggerHiddenAttribute]
        protected void Loguear(string mensaje)
        {
            string[] renglones = mensaje.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string renglon in renglones)
            {
                if (OnLoguearVisualmente != null)
                    OnLoguearVisualmente(renglon);
            }
        }

        [DebuggerHidden]
        protected void Loguear(string Titulo, string mensaje)
        {
            Loguear(new string('*', 50).Replace("*", "*-") + Titulo.ToUpper() + new string('*', 50).Replace("*", "*-"));
            Loguear(mensaje);
            Loguear(new string('*', 200));
        }

        protected void LoguearEstadisticas(SyncOperationStatistics estadisticas, DbSyncScopeDescription ambito)
        {
            string message = "";
            message = message + "\n\rEstadisticas de la replica de Sync Framework ambito " + ambito.ScopeName + " cant. tablas: " + ambito.Tables.Count + "";
            message = message + "\n\r\tHora de inicio:\t" + estadisticas.SyncStartTime.ToString();
            message = message + "\n\r\tHora de fin:\t" + estadisticas.SyncEndTime.ToString();
            message = message + "\n\r\tSubida cambios aplicados\t:" + estadisticas.UploadChangesApplied.ToString();
            message = message + "\n\r\tSubida cambios c/error\t:" + estadisticas.UploadChangesFailed.ToString();
            message = message + "\n\r\tSubida cambios Total\t:" + estadisticas.UploadChangesTotal.ToString();
            message = message + "\n\r\tBajada cambios aplicados\t:" + estadisticas.DownloadChangesApplied.ToString();
            message = message + "\n\r\tBajada cambios c/error\t:" + estadisticas.DownloadChangesFailed.ToString();
            message = message + "\n\r\tBajada cambios Total\t:" + estadisticas.DownloadChangesTotal.ToString();

            Loguear("Estadisticas", message);
        }

        protected void Orchestrator_SessionProgress(object sender, SyncStagedProgressEventArgs e)
        {
            String mensaje =
               string.Format(
               "{0}: {1} {2}: {3} {4}: {5} {6}: {7}",
               "Trabajo terminado", e.CompletedWork,
               "\tProveedor que reporta", e.ReportingProvider,
               "\tEstado: ", e.Stage,
               "\tTrabajo total: ", e.TotalWork
               );

            Loguear("orchestrator_SessionProgress", mensaje);
        }

        protected void Orchestrator_StateChanged(object sender, SyncOrchestratorStateChangedEventArgs e)
        {
            String mensaje =
                string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r",
                "\tNuevo estado Del Orquestador", e.NewState.ToString(),
                "\tAnterior estado Del Orquestador", e.OldState.ToString()
                );
            Loguear("orchestrator_StateChanged", mensaje);
        }

        protected void Proveedor_ApplyChangeFailed(object sender, DbApplyChangeFailedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r{6}:{7}\n\r{8}:{9}\n\r{10}:{11}\n\r",
                "\tSource Database  :", e.Connection.Database,
                "\tContexto         :", e.Context,
                "\tAction           :", e.Action.ToString(),
                "\tSession Id       :", e.Session.SessionId,
                "\tTransaccion      :", e.Transaction.ToString(),
                "\tTipo de conflicto: ", e.Conflict.Type
                );
            e.Action = ApplyAction.RetryWithForceWrite; //Aplicar cambios del lado del local

            Loguear("proveedor_ApplyChangeFailed", mensaje);
        }

        protected void Proveedor_ApplyingChanges(object sender, DbApplyingChangesEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r{6}:{7}\n\r{8}:{9}\n\r",
                "\tSource Database :", e.Connection.Database,
                "\tContexto        :", e.Context,
                "\tBatch count     :", e.ScopeMetadata.BatchCount,
                "\tSession Id      :", e.Session.SessionId,
                "\tTransaccion     :", e.Transaction.ToString()
                );

            Loguear("proveedor_ApplyingChanges", mensaje);
        }

        protected void Proveedor_ApplyMetadataFailed(object sender, ApplyMetadataFailedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r{6}:{7}\n\r{8}:{9}\n\r",
                "\tSource Database :", e.Connection.Database,
                "\tContexto        :", e.Context,
                "\tError           :", e.Error.ToString(),
                "\tSession Id      :", e.Session.SessionId,
                "\tTransaccion     :", e.Transaction.ToString()
                );

            Loguear("proveedor_ApplyMetadataFailed", mensaje);
        }

        protected void Proveedor_BatchApplied(object sender, DbBatchAppliedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r",
                "\tDestination Database:", ((RelationalSyncProvider)sender).Connection.Database,
                "\tBatch Number:", e.CurrentBatchNumber,
                "\tTotal Batches To Apply :", e.TotalBatchesToApply);

            Loguear("Proveedor_BatchApplied", mensaje);
        }

        protected void Proveedor_BatchSpooled(object sender, DbBatchSpooledEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r{6}:{7}\n\r{8}:{9}\n\r{10}:{11}\n\r",
                "\tSource Database :", ((RelationalSyncProvider)sender).Connection.Database,
                "\tBatch Name      :", e.BatchFileName,
                "\tBatch Size      :", e.DataCacheSize,
                "\tBatch Number    :", e.CurrentBatchNumber,
                "\tTotal Batches   :", e.TotalBatchesSpooled,
                "\tBatch Watermark :", ReadTableWatermarks(e.CurrentBatchTableWatermarks));

            Loguear("Proveedor_BatchSpooled", mensaje);
        }

        protected void Proveedor_ChangesApplied(object sender, DbChangesAppliedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r{6}:{7}\n\r",
                "\tSource Database :", e.Connection.Database,
                "\tContexto        :", e.Context,
                "\tSession Id      :", e.Session.SessionId,
                "\tTransaccion     :", e.Transaction.ToString()
                );

            Loguear("proveedor_ChangesApplied", mensaje);
        }

        protected void proveedor_ChangesSelected(object sender, DbChangesSelectedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r{6}:{7}\n\r{8}:{9}\n\r",
                "\tSource Database :", e.Connection.Database,
                "\tContexto        :", e.Context,
                "\tBatch count     :", e.ScopeMetadata.BatchCount,
                "\tSession Id      :", e.Session.SessionId,
                "\tTransaccion     :", e.Transaction.ToString());

            Loguear("proveedor_ChangesSelected", mensaje);
        }

        protected void Proveedor_DbConnectionFailure(object sender, DbConnectionFailureEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r",
                "\tAccion :", e.Action,
                "\tError de conexion:", e.FailureException.Message,
                "\tReintentos       :", e.RetryCount.ToString()
                );

            Loguear("proveedor_DbConnectionFailure", mensaje);
        }

        protected void Proveedor_SelectingChanges(object sender, DbSelectingChangesEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}",
                "\tIsolation Level :", e.Transaction.IsolationLevel,
                "\tTotal actualizados:", e.Context.ScopeProgress.TotalUpdates.ToString()
                );

            Loguear("proveedor_SelectingChanges", mensaje);
        }

        protected void Proveedor_SyncPeerOutdated(object sender, DbOutdatedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}",
                "\tAccion :", e.Action,
                "\tId session:", e.Session.SessionId.ToString()
                );

            Loguear("proveedor_SyncPeerOutdated", mensaje);
        }

        protected void Proveedor_SyncProgress(object sender, DbSyncProgressEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r{6}:{7}",
                "\tEstado :", e.Stage,
                "\tCantidad de cambios Aplicados:", e.TableProgress.ChangesApplied.ToString(),
                "\tCantidad de cambios Pendientes:", e.TableProgress.ChangesPending.ToString(),
                "\tCantidad de cambios:", e.TableProgress.TotalChanges.ToString()
                );

            Loguear("proveedor_SyncProgress", mensaje);
        }

        protected string ReadTableWatermarks(Dictionary<string, ulong> dictionary)
        {
            StringBuilder builder = new StringBuilder();
            Dictionary<string, ulong> dictionaryClone = new Dictionary<string, ulong>(dictionary);
            foreach (KeyValuePair<string, ulong> kvp in dictionaryClone)
            {
                builder.Append(kvp.Key).Append(":").Append(kvp.Value).Append(",");
            }
            return builder.ToString();
        }

        private void ActualizarAnclaDeSincronizacion(int anclaActual, DbSyncScopeDescription ambito, IDbConnection conexionLocalSql)
        {
            string tablaAnclas = ObtenerNombreTablaAnclas();

            int ultimaAnclaDeSincronizacion = ObtenerUltimaAnclaDeSincronizacion(ambito, conexionLocalSql);
            string comando = "";
            if (ultimaAnclaDeSincronizacion >= 0)
            {
                comando = "update " + tablaAnclas + " set  last_anchor_sync = " + anclaActual +
                        ", last_sync_datetime = getdate() where sync_scope_name = '" + ambito.ScopeName + "'" +
                         " and sync_remote_DataBase_name = '" + _nombreDeBaseRemota + "'";
            }
            else
            {
                comando = "insert into " + tablaAnclas +
                    " ( sync_scope_name, last_anchor_sync, last_sync_datetime, sync_remote_DataBase_name ) values " +
                    "('" + ambito.ScopeName + "'," + anclaActual.ToString() + ", getdate(), '" + _nombreDeBaseRemota + "' )";
            }

            using (SqlCommand consulta = new SqlCommand(comando, (SqlConnection)conexionLocalSql))
            {
                consulta.ExecuteNonQuery();
            }
        }

        private void AprovicionarAmbito(DbSyncScopeDescription Ambito, SqlSyncScopeProvisioning proveedorDeAmbito)
        {
            if (proveedorDeAmbito.ScopeExists(Ambito.ScopeName))
            {
                Loguear("El ambito " + Ambito.ScopeName + " ya existe!!");
            }
            else
            {
                Loguear("Se va a crear el ambito " + Ambito.ScopeName);

                proveedorDeAmbito.PopulateFromScopeDescription(Ambito);
                proveedorDeAmbito.SetCreateTableDefault(DbSyncCreationOption.CreateOrUseExisting);
                proveedorDeAmbito.Apply();

                Loguear("Se creo el ambito " + Ambito.ScopeName);
            }
        }

        private IDbConnection GetSQLConnection(string stringConnection)
        {
            var sqlConnection = new SqlConnection(stringConnection);

            try
            {
                sqlConnection.Open();
            }
            catch (Exception e)
            {
                Loguear("¡Error! al abrir conexión!!: " + e.ToString());
            }
            return sqlConnection;
        }

        
        private void ControlarEsquemas(IDbConnection sqlConnection, string esquemaQueSeReplica)
        {
            ControladorEsquemas controlador = new ControladorEsquemas();
            if (sqlConnection.State != System.Data.ConnectionState.Open)
            {
                Loguear("Conexion no esta disponible. Imposible validar esquemas. ["+ sqlConnection.Database+ "]");
            }
            else
            {
                controlador.Controlar((SqlConnection)sqlConnection, sqlConnection.Database, esquemaQueSeReplica);
                controlador.Controlar((SqlConnection)sqlConnection, sqlConnection.Database, _esquemaMetadataSyncFramework);
            }
        }

        private DbSyncScopeDescription CrearAmbito(String nombreDeAmbito, string TablaDentroDelAmbito, IDbConnection conexionSql)
        {
            DbSyncScopeDescription Ambito = new DbSyncScopeDescription(nombreDeAmbito);
            DbSyncTableDescription descripcionDeTabla = SqlSyncDescriptionBuilder.GetDescriptionForTable(TablaDentroDelAmbito, (SqlConnection)conexionSql);
            Ambito.Tables.Add(descripcionDeTabla);
            return Ambito;
        }

        private SqlSyncScopeProvisioning CrearProveedorDeAmbito(String esquemaMetadataSyncFramework, String prefijoMetadataSyncFramework, IDbConnection conexionSql, DbSyncScopeDescription Ambito)
        {
            SqlSyncScopeProvisioning proveedorDeAmbito = new SqlSyncScopeProvisioning((SqlConnection)conexionSql, Ambito);
            proveedorDeAmbito.ObjectSchema = esquemaMetadataSyncFramework;
            proveedorDeAmbito.ObjectPrefix = prefijoMetadataSyncFramework;
            return proveedorDeAmbito;
        }

        private void DesaprovicionarAmbito(IDbConnection conexionSql, DbSyncScopeDescription Ambito, SqlSyncScopeProvisioning proveedorDeAmbito)
        {
            SqlSyncScopeDeprovisioning DesaprovicionadorDeAmbito = new SqlSyncScopeDeprovisioning((SqlConnection)conexionSql);
            DesaprovicionadorDeAmbito.ObjectSchema = _esquemaMetadataSyncFramework;
            DesaprovicionadorDeAmbito.ObjectPrefix = _prefijoMetadataSyncFramework;

            lock (_lockAprovisionamiento)
            {
                if (proveedorDeAmbito.ScopeExists(Ambito.ScopeName))
                {
                    Loguear("Se va a eliminar el ambito " + Ambito.ScopeName);
                    DesaprovicionadorDeAmbito.DeprovisionScope(Ambito.ScopeName);
                    Loguear("Se elimino el ambito " + Ambito.ScopeName);
                }
                else
                    Loguear("No se elimina el ambito " + Ambito.ScopeName + " [no existe]" );
            }
            
        }

        private void DesaprovicionarServidor(IDbConnection sqlConnection)
        {
            SqlSyncScopeDeprovisioning DesaprovicionadorDeAmbito = new SqlSyncScopeDeprovisioning((SqlConnection)sqlConnection)
            {
                ObjectSchema = _esquemaMetadataSyncFramework,
                ObjectPrefix = _prefijoMetadataSyncFramework
            };
            try
            {
                Loguear("Se va a desaprovicionar el servidor entero " + sqlConnection.Database);
                DesaprovicionadorDeAmbito.DeprovisionStore();
                Loguear("Se desaproviciono el servidor entero " + sqlConnection.Database);
            }
            catch (Exception e)
            {
                Loguear(e.Message);
            }
        }

        private bool ElAmbitoTieneNovedades(DbSyncScopeDescription ambito, IDbConnection conexionLocalSql)
        {
            bool retorno = false;

            string tablaAnclas = ObtenerNombreTablaAnclas();
            int anclaDeLaUltimaSincronizacion = ObtenerUltimaAnclaDeSincronizacion(ambito, conexionLocalSql);

            string procedimientoAlmacenado = ambito.Tables[0].UnquotedGlobalName + "_selectchanges";
            SqlCommand cmd = new SqlCommand(procedimientoAlmacenado, (SqlConnection)conexionLocalSql)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add(new SqlParameter("@sync_min_timestamp", anclaDeLaUltimaSincronizacion));
            cmd.Parameters.Add(new SqlParameter("@sync_scope_local_id", 1));
            cmd.Parameters.Add(new SqlParameter("@sync_scope_restore_count", 1));
            cmd.Parameters.Add(new SqlParameter("@sync_update_peer_key", 1));

            SqlDataReader rdr = cmd.ExecuteReader();
            retorno = rdr.HasRows;
            rdr.Close();
            if (!retorno)
            {
                Loguear("El ambito: " + ambito.ScopeName + " no tiene novedades desde el ancla: " + anclaDeLaUltimaSincronizacion);
            }

            return retorno;
        }

        private bool ExisteAmbito(string esquemaMetadataSyncFramework, string prefijoMetadataSyncFramework, SqlConnection conexionSql, DbSyncScopeDescription ambito)
        {
            Boolean resultado = false;

            SqlSyncScopeProvisioning serverConfig = new SqlSyncScopeProvisioning(conexionSql);
            serverConfig.ObjectPrefix = prefijoMetadataSyncFramework;
            serverConfig.ObjectSchema = esquemaMetadataSyncFramework;
            resultado = serverConfig.ScopeExists(ambito.ScopeName);
            return resultado;
        }

        private void InicializarVariablesDeSeteoSync(ParametrosReplica parametros)
        {
            _prefijoParaNombreDeAmbito = parametros.prefijoParaNombreDeAmbito;
            Loguear("Prefijo para nombre de Ambito: " + _prefijoParaNombreDeAmbito);
            _esquemaMetadataSyncFramework = parametros.esquemaMetadataSyncFramework;
            Loguear("Esquema para Metadata de Sync Framework: " + _esquemaMetadataSyncFramework);
            _prefijoMetadataSyncFramework = parametros.prefijoMetadataSyncFramework;
            Loguear("Prefijo para Metadata de Sync Framework: " + _prefijoMetadataSyncFramework);
            _esquemaQueSeReplica = parametros.esquemaQueSeReplica;
            Loguear("Esquema de la base que se replica: " + parametros.esquemaQueSeReplica);
            _suscribirATodosLosEventos = parametros.SuscribirseATodosLosEventosDeInformacion;
        }

        private void LoguearNivelDeTracingConfigurador()
        {
            if (SyncTracer.IsErrorEnabled() || SyncTracer.IsWarningEnabled() || SyncTracer.IsInfoEnabled() || SyncTracer.IsVerboseEnabled())
            {
                Loguear("Tracing activado! revisar en config cual es el archivo.");
            }
            else
            {
                Loguear("Tracing desactivado, activar en el config.");
            }

            if (SyncTracer.IsErrorEnabled())
                Loguear("Tracing de errores Activado");

            if (SyncTracer.IsWarningEnabled())
                Loguear("Tracing de advertencias Activado");

            if (SyncTracer.IsInfoEnabled())
                Loguear("Tracing de información Activado");

            if (SyncTracer.IsVerboseEnabled())
                Loguear("Tracing de todo Activado");
        }

        private void MantenerAprovisionamientoDeAmbitos(ParametrosReplica parametros, string esquemaMetadataSyncFramework, string prefijoMetadataSyncFramework, DbSyncScopeDescription ambito)
        {
            _semaphore.WaitOne();
            _hilosAprovisionando++;
            try
            {
                IDbConnection localSQLConnection = null;
                IDbConnection RemoteSQLConnection = null;
                SqlSyncScopeProvisioning proveedorDeAmbitoLocal = null;
                SqlSyncScopeProvisioning proveedorDeAmbitoRemoto = null;

                if (parametros.DesaprovisionarAmbitosEnServidorLocal || parametros.AprovisionarAmbitosEnServidorLocal)
                {
                    localSQLConnection = GetSQLConnection(parametros.StringConnectionLocal);
                    proveedorDeAmbitoLocal = CrearProveedorDeAmbito(esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, localSQLConnection, ambito);
                    proveedorDeAmbitoLocal.CommandTimeout = parametros.TimeOut;
                }
                if (parametros.DesaprovisionarAmbitosEnServidorRemoto || parametros.AprovisionarAmbitosEnServidorRemoto)
                {
                    RemoteSQLConnection = GetSQLConnection(parametros.StringConnectionRemoto);
                    proveedorDeAmbitoRemoto = CrearProveedorDeAmbito(esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, RemoteSQLConnection, ambito);
                    proveedorDeAmbitoRemoto.CommandTimeout = parametros.TimeOut;
                }

                
                if (parametros.DesaprovisionarAmbitosEnServidorLocal)
                    DesaprovicionarAmbito(localSQLConnection, ambito, proveedorDeAmbitoLocal);

                if (parametros.DesaprovisionarAmbitosEnServidorRemoto)
                    DesaprovicionarAmbito(RemoteSQLConnection, ambito, proveedorDeAmbitoRemoto);

                if (parametros.AprovisionarAmbitosEnServidorLocal)
                    AprovicionarAmbito(ambito, proveedorDeAmbitoLocal);

                if (parametros.AprovisionarAmbitosEnServidorRemoto)
                    AprovicionarAmbito(ambito, proveedorDeAmbitoRemoto);

                if (localSQLConnection != null && localSQLConnection.State == ConnectionState.Open)
                    localSQLConnection.Close();

                if (RemoteSQLConnection != null && RemoteSQLConnection.State == ConnectionState.Open)
                    RemoteSQLConnection.Close();
            }
            catch (Exception errorcito)
            {
                Loguear("Error manteniendo ambitos!!: " + errorcito.ToString());
            }
            finally
            {
                _semaphore.Release(1);
                _hilosAprovisionando--;
            }
        }

        private void MantenerAprovisionamientoDeAmbitosEnHilos(ParametrosReplica parametros, string esquemaMetadataSyncFramework, string prefijoMetadataSyncFramework, List<DbSyncScopeDescription> Ambitos)
        {
            _semaphore = new Semaphore(parametros.HilosParaAprovisionar, parametros.HilosParaAprovisionar);
            _hilosAprovisionando = 0;

            foreach (DbSyncScopeDescription ambito in Ambitos)
            {
                DbSyncScopeDescription LocalAmbito = ambito;

                Thread t = new Thread(()=>
                {
                    MantenerAprovisionamientoDeAmbitos(parametros, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, LocalAmbito);
                });
                t.Start();
            }

            while (_hilosAprovisionando != 0)
            {
                Thread.Sleep(500);
            }
        }

        private void MantenerTablaDeAnclasLocal(IDbConnection sqlLocalConnection)
        {
            var localConnection = (SqlConnection)sqlLocalConnection;
            if (localConnection.State != System.Data.ConnectionState.Open)
            {
                throw new Exception("No se puede mantener la tabla de anclas sin no esta la conexion local abierta.");
            }

            string comando = "select s.name, o.name from sys.objects o inner join sys.schemas s on s.schema_id = o.schema_id " +
                              "where s.name = '" + _esquemaMetadataSyncFramework +
                                        "' and o.name = '" + _prefijoMetadataSyncFramework + "_anchors'";
            using (SqlCommand consulta = new SqlCommand(comando, localConnection))
            {
                using (SqlDataReader readerEsquema = consulta.ExecuteReader())
                {
                    if (!readerEsquema.HasRows)
                    {
                        readerEsquema.Close();
                        comando = "CREATE TABLE " + ObtenerNombreTablaAnclas() +
                                " ([id] [int] IDENTITY(1,1) NOT NULL, " +
                                "  [sync_scope_name] [nvarchar](100) NOT NULL, " +
                                "  [sync_remote_DataBase_name] [nvarchar](200) NOT NULL, " +
                                "        [last_anchor_sync] [int] NOT NULL, " +
                                "        [last_sync_datetime] [datetime] NOT NULL " +
                                "    ) ON [PRIMARY]";
                        using (SqlCommand CrearEsquema = new SqlCommand(comando, localConnection))
                        {
                            CrearEsquema.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private List<DbSyncScopeDescription> ObtenerAmbitosAProcesar(ParametrosReplica parametros)
        {
            Loguear("Inicia proceso de obtener ambitos a procesar.");
            List<DbSyncScopeDescription> ambitos = new List<DbSyncScopeDescription>();
            var strConexionSql = (parametros.UsarDescripcionLocal) ? parametros.StringConnectionLocal : parametros.StringConnectionRemoto;
            var connectionSQL = GetSQLConnection(strConexionSql);
            foreach (string tabla in parametros.ListaDeTablas)
            {
                var tablaSplit = tabla.Split('.');
                var schema = tablaSplit[0];
                var nombreTabla = tablaSplit[1];
                var scopeName = String.Format(_prefijoParaNombreDeAmbito, schema, nombreTabla);

                try
                {
                    var ambito = CrearAmbito(scopeName, tabla, connectionSQL);
                    ambitos.Add(ambito);
                }
                catch (Exception e)
                {
                    Loguear($"Error al crear el ambito: [{scopeName}] /r/n{e.ToString()}");
                }

            }
            Loguear("Finalizado proceso de obtener ambitos a procesar, ambitos creados: " + ambitos.Count);
            return ambitos;
        }

        private int ObtenerAnclaActual(IDbConnection conexionLocalSql)
        {
            int ancla = 0;
            using (SqlCommand consulta = new SqlCommand("select min_active_rowversion() - 1", (SqlConnection)conexionLocalSql))
            {
                using (SqlDataReader reader = consulta.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        if (reader.Read())
                        {
                            ancla = reader.GetInt32(0);
                        }
                    }
                    else
                    {
                        throw new Exception("No se puede obtener el ancla actual. La consulta no trae registros.");
                    }
                }
            }

            return ancla;
        }

        private string ObtenerNombreTablaAnclas()
        {
            return _esquemaMetadataSyncFramework + "." + _prefijoMetadataSyncFramework + "_anchors";
        }

        private SyncOrchestrator ObtenerOrquestadorDeReplica(ParametrosReplica parametros)
        {
            SyncOrchestrator orchestrator = new SyncOrchestrator();
            if (parametros.SitioDeSubida)
                orchestrator.Direction = SyncDirectionOrder.Upload;
            else
                orchestrator.Direction = SyncDirectionOrder.Download;

            if (_suscribirATodosLosEventos)
            {
                orchestrator.SessionProgress += new EventHandler<SyncStagedProgressEventArgs>(Orchestrator_SessionProgress);
                orchestrator.StateChanged += new EventHandler<SyncOrchestratorStateChangedEventArgs>(Orchestrator_StateChanged);
            }
            return orchestrator;
        }
        private SqlSyncProvider ObtenerProveedor(String nombreDeAmbito, IDbConnection conexionSql, uint tamañoDeCache, uint tamañoTransaccion)
        {
            SqlSyncProvider proveedor = new SqlSyncProvider(nombreDeAmbito, (SqlConnection)conexionSql, _prefijoMetadataSyncFramework, _esquemaMetadataSyncFramework);

            if (tamañoDeCache > 0)
            {
                proveedor.MemoryDataCacheSize = tamañoDeCache; //KB --> los archivos de cache se guardan en BatchingDirectory (%tmp% por default)
                proveedor.ApplicationTransactionSize = tamañoTransaccion;
            }
            proveedor.Connection = conexionSql;

            proveedor.ApplyChangeFailed += new EventHandler<DbApplyChangeFailedEventArgs>(Proveedor_ApplyChangeFailed); //Este tiene logica importante, no quitar.

            if (_suscribirATodosLosEventos)
            {
                proveedor.BatchApplied += new EventHandler<DbBatchAppliedEventArgs>(Proveedor_BatchApplied);
                proveedor.BatchSpooled += new EventHandler<DbBatchSpooledEventArgs>(Proveedor_BatchSpooled);
                proveedor.ChangesSelected += new EventHandler<DbChangesSelectedEventArgs>(proveedor_ChangesSelected);
                proveedor.ApplyingChanges += new EventHandler<DbApplyingChangesEventArgs>(Proveedor_ApplyingChanges);

                proveedor.ApplyMetadataFailed += new EventHandler<ApplyMetadataFailedEventArgs>(Proveedor_ApplyMetadataFailed);
                proveedor.ChangesApplied += new EventHandler<DbChangesAppliedEventArgs>(Proveedor_ChangesApplied);

                proveedor.DbConnectionFailure += new EventHandler<DbConnectionFailureEventArgs>(Proveedor_DbConnectionFailure);
                proveedor.SelectingChanges += new EventHandler<DbSelectingChangesEventArgs>(Proveedor_SelectingChanges);
                proveedor.SyncPeerOutdated += new EventHandler<DbOutdatedEventArgs>(Proveedor_SyncPeerOutdated);
                proveedor.SyncProgress += new EventHandler<DbSyncProgressEventArgs>(Proveedor_SyncProgress);
            }
            return proveedor;
        }
        private int ObtenerUltimaAnclaDeSincronizacion(DbSyncScopeDescription ambito, IDbConnection sqlLocalConnection)
        {
            string tablaAnclas = ObtenerNombreTablaAnclas();
            string comando = "select last_anchor_sync from " + tablaAnclas +
                " where sync_scope_name = '" + ambito.ScopeName + "'" +
                " and sync_remote_DataBase_name = '" + _nombreDeBaseRemota + "'";

            int ancla = -1;
            using (SqlCommand consulta = new SqlCommand(comando, (SqlConnection)sqlLocalConnection))
            {
                using (SqlDataReader reader = consulta.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        ancla = reader.GetInt32(0);
                    }
                }
            }
            return ancla;
        }

        private void ReplicarAmbitos(List<DbSyncScopeDescription> Ambitos, ParametrosReplica parameters) //int timeOut, uint tamañoDeCache)
        {
            _replicationSemaphore = new Semaphore(parameters.HilosParaReplicar, parameters.HilosParaReplicar);
            foreach (DbSyncScopeDescription ambito in Ambitos)
            {
                try
                {
                    _replicationSemaphore.WaitOne();
                    Loguear("Inicia thread para replicar el ambito: " + ambito.ScopeName);
                    Thread t = new Thread(()=>
                    {
                        StartReplicationOfOneScope(parameters, ambito);
                    });
                    HilosEnEjecucion.Add(t);
                    t.Start();

                }
                catch (Exception error)
                {
                    Loguear("Error al replicar el ambito: " + ambito.ScopeName + " Error: " + error.ToString());
                }
            }

            var threadCount = 1;
            while (threadCount > 0)
            {
                Thread.Sleep(500);
                threadCount = HilosEnEjecucion.FindAll(x => x.IsAlive).Count;
                Loguear($"Hilos en ejecución: {threadCount}");
            }

        }

        private void StartReplicationOfOneScope(ParametrosReplica parameters, DbSyncScopeDescription ambito)
        {
            try
            {
                SyncOrchestrator orchestrator = ObtenerOrquestadorDeReplica(parameters);
                var conexionLocalSql = GetSQLConnection(parameters.StringConnectionLocal);
                var conexionRemotoSql = GetSQLConnection(parameters.StringConnectionRemoto);

                var proveedorLocal = ObtenerProveedor(ambito.ScopeName, conexionLocalSql, parameters.TamañoDeCache, parameters.TamañoDeTransaccion);
                var proveedorRemoto = ObtenerProveedor(ambito.ScopeName, conexionRemotoSql, parameters.TamañoDeCache, parameters.TamañoDeTransaccion);

                proveedorLocal.CommandTimeout = parameters.TimeOut;
                proveedorRemoto.CommandTimeout = parameters.TimeOut;

                orchestrator.LocalProvider = proveedorLocal;
                orchestrator.RemoteProvider = proveedorRemoto;

                var replicar = true;
                int anclaActual = ObtenerAnclaActual(conexionLocalSql);

                if (parameters.ReplicarSoloAmbitosconCambios)
                    replicar = ElAmbitoTieneNovedades(ambito, conexionLocalSql);

                if (replicar)
                {
                    SyncOperationStatistics statsUpload = orchestrator.Synchronize();
                    LoguearEstadisticas(statsUpload, ambito);
                    ActualizarAnclaDeSincronizacion(anclaActual, ambito, conexionLocalSql);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                _replicationSemaphore.Release(1);
            } 

        }
    }
}
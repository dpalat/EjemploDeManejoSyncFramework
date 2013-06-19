using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Data;
using Microsoft.Synchronization.Data.SqlServer;
using Microsoft.Synchronization.Data;
using Microsoft.Synchronization;
using System.Transactions;
using System.Threading;
using System.Diagnostics;

namespace EjemploDeManejoSyncFramework
{
    public class ManagerSyncFramework
    {
        private readonly Object obj = new Object();
        private Semaphore semaforo;
        private int HilosAprovisionando = 0;
        private SqlConnection conexionLocalSql;
        private SqlConnection conexionRemotoSql;
        private string prefijoParaNombreDeAmbito;
        private string esquemaMetadataSyncFramework;
        private string prefijoMetadataSyncFramework;
        private string esquemaQueSeReplica;
        private bool SuscribirATodosLosEventos;

        public delegate void LoguearEventHandler( string renglon );
        public delegate void replicaFinalizada();
        public event LoguearEventHandler onLoguearVisualmente;
        public event replicaFinalizada onProcesoFinalizado;
        private string nombreDeBaseRemota;
        public string TiempoTotalTranscurrido { get; set; }

        
        public void IniciarReplica( ParametrosReplica parametrosReplica )
        {
            Stopwatch stopWatch = new Stopwatch(); 
            stopWatch.Start();

            //Inicializa las propiedades de esquemas y prefijos en la clase.
            this.InicializarVariablesDeSeteoSync( parametrosReplica );
            
            //Si se tiene o no configurado el tracing en el app.config, se va a loguear.
            this.loguearNivelDeTracingConfigurador();
            
            //Conecta con los endpoints
            this.ConectarConSQLServer( parametrosReplica.StringConnectionLocal, parametrosReplica.StringConnectionRemoto );

            //Crea los esquemas segun corresponda.
            this.ControlarEsquemas( parametrosReplica.esquemaQueSeReplica);

            //Limpia toda la metadata relacionada a Sync Framework de ambos endpoints segun corresponda.
            if (parametrosReplica.LimpiarServidorLocal)  this.DesaprovicionarServidor( this.conexionLocalSql );
            if (parametrosReplica.LimpiarServidorRemoto) this.DesaprovicionarServidor( this.conexionRemotoSql );
            
            //Obtiene los ambitos seleccionados.
            List<DbSyncScopeDescription> Ambitos = this.ObtenerAmbitosAProcesar(parametrosReplica);
            
            //Elimina y crea los ambitos segun corresponda con lo deseado.
            this.MantenerAprovisionamientoDeAmbitosEnHilos(parametrosReplica, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, Ambitos);
            this.nombreDeBaseRemota = this.conexionRemotoSql.Database;
            this.MantenerTablaDeAnclasLocal();

            //Inicia proceso de replica
            if (parametrosReplica.RealizarReplica)
            {
                SyncOrchestrator orchestrator = this.ObtenerOrquestadorDeReplica(parametrosReplica);
                this.ReplicarAmbitos( orchestrator, Ambitos, parametrosReplica);
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            this.TiempoTotalTranscurrido = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);

            if ( this.onProcesoFinalizado != null )
                this.onProcesoFinalizado();
        }

        private void MantenerTablaDeAnclasLocal()
        {
            if (this.conexionLocalSql.State != System.Data.ConnectionState.Open)
            {
                throw new Exception("No se puede mantener la tabla de anclas sin no esta la conexion local abierta.");
            }
            
            string comando = "select s.name, o.name from sys.objects o inner join sys.schemas s on s.schema_id = o.schema_id " +
                              "where s.name = '" + this.esquemaMetadataSyncFramework +
                                        "' and o.name = '" + this.prefijoMetadataSyncFramework + "_anchors'";
            using (SqlCommand consulta = new SqlCommand( comando, this.conexionLocalSql))
            {
                using (SqlDataReader readerEsquema = consulta.ExecuteReader())
                {
                    if (!readerEsquema.HasRows)
                    {
                        readerEsquema.Close();
                        comando = "CREATE TABLE " + this.ObtenerNombreTablaAnclas() +
                                " ([id] [int] IDENTITY(1,1) NOT NULL, " +
                                "  [sync_scope_name] [nvarchar](100) NOT NULL, " +
                                "  [sync_remote_DataBase_name] [nvarchar](200) NOT NULL, " +
                                "        [last_anchor_sync] [int] NOT NULL, " +
                                "        [last_sync_datetime] [datetime] NOT NULL " +
                                "    ) ON [PRIMARY]";
                        using (SqlCommand CrearEsquema = new SqlCommand( comando, this.conexionLocalSql ))
                        {
                            CrearEsquema.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private string ObtenerNombreTablaAnclas()
        {
            return this.esquemaMetadataSyncFramework + "." + this.prefijoMetadataSyncFramework + "_anchors";
        }

        private SyncOrchestrator ObtenerOrquestadorDeReplica(ParametrosReplica parametros)
        {
            SyncOrchestrator orchestrator = new SyncOrchestrator();
            if (parametros.SitioDeSubida)
                orchestrator.Direction = SyncDirectionOrder.Upload;
            else
                orchestrator.Direction = SyncDirectionOrder.Download;

            if (this.SuscribirATodosLosEventos)
            {
                orchestrator.SessionProgress += new EventHandler<SyncStagedProgressEventArgs>(this.orchestrator_SessionProgress);
                orchestrator.StateChanged += new EventHandler<SyncOrchestratorStateChangedEventArgs>(this.orchestrator_StateChanged);
            }
            return orchestrator;
        }

        private List<DbSyncScopeDescription> ObtenerAmbitosAProcesar(ParametrosReplica parametros )
        {
            List<DbSyncScopeDescription> Ambitos = new List<DbSyncScopeDescription>();
            foreach (string tabla in parametros.ListaDeTablas)
            {
                var tablaSplit = tabla.Split('.');
                string schema = tablaSplit[0];
                string NombreTabla = tablaSplit[1];
                var conexionSql = (parametros.SitioDeSubida) ? conexionLocalSql : conexionRemotoSql;
                Ambitos.Add(this.CrearAmbito(String.Format(this.prefijoParaNombreDeAmbito, schema, NombreTabla), tabla, conexionSql));
            }
            return Ambitos;
        }

        private void MantenerAprovisionamientoDeAmbitos(ParametrosReplica parametros, string esquemaMetadataSyncFramework, string prefijoMetadataSyncFramework, DbSyncScopeDescription ambito)
        {
            this.semaforo.WaitOne();
            this.HilosAprovisionando++;
            try
            {
                SqlConnection NuevaConexionLocalSql = null;
                SqlConnection NuevaConexionRemotoSql = null;
                SqlSyncScopeProvisioning proveedorDeAmbitoLocal = null;
                SqlSyncScopeProvisioning proveedorDeAmbitoRemoto = null;

                if (parametros.DesaprovisionarAmbitosEnServidorLocal || parametros.AprovisionarAmbitosEnServidorLocal)
                {
                    try{
                        NuevaConexionLocalSql = new SqlConnection(parametros.StringConnectionLocal);
                        NuevaConexionLocalSql.Open();
                    }
                    catch (Exception er){
                        this.loguear("Error al abrir conexión local al crear al aprovisionador!!: " + er.ToString());
                    }
                    proveedorDeAmbitoLocal = this.CrearProveedorDeAmbito(esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, NuevaConexionLocalSql, ambito);
                    proveedorDeAmbitoLocal.CommandTimeout = parametros.TimeOut;
                }
                if (parametros.DesaprovisionarAmbitosEnServidorLocal || parametros.AprovisionarAmbitosEnServidorRemoto )
                {
                    try
                    {
                        NuevaConexionRemotoSql = new SqlConnection(parametros.StringConnectionRemoto);
                        NuevaConexionRemotoSql.Open();
                    }
                    catch (Exception er)
                    {
                        this.loguear("Error al abrir conexión remota al crear al aprovisionador!!: " + er.ToString());
                    }
                    proveedorDeAmbitoRemoto = this.CrearProveedorDeAmbito(esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, NuevaConexionRemotoSql, ambito);
                    proveedorDeAmbitoRemoto.CommandTimeout = parametros.TimeOut;
                }

                if ( parametros.DesaprovisionarAmbitosEnServidorLocal)
                    this.DesaprovicionarAmbito(NuevaConexionLocalSql, ambito, proveedorDeAmbitoLocal);

                if ( parametros.DesaprovisionarAmbitosEnServidorRemoto)
                    this.DesaprovicionarAmbito(NuevaConexionRemotoSql, ambito, proveedorDeAmbitoRemoto);

                if ( parametros.AprovisionarAmbitosEnServidorLocal  )
                    this.AprovicionarAmbito(ambito, proveedorDeAmbitoLocal);

                if ( parametros.AprovisionarAmbitosEnServidorRemoto)
                    this.AprovicionarAmbito(ambito, proveedorDeAmbitoRemoto);

                if (NuevaConexionLocalSql != null && NuevaConexionLocalSql.State == ConnectionState.Open)
                    NuevaConexionLocalSql.Close();

                if (NuevaConexionRemotoSql != null && NuevaConexionRemotoSql.State == ConnectionState.Open)
                    NuevaConexionRemotoSql.Close();
            }
            catch (Exception errorcito)
            {
                this.loguear("Error manteniendo ambitos!!: " + errorcito.ToString());
            }
            finally
            {
                this.semaforo.Release(1);
                this.HilosAprovisionando--;            
            }
        }

        private void AprovicionarAmbito(DbSyncScopeDescription Ambito, SqlSyncScopeProvisioning proveedorDeAmbito)
        {
            if (proveedorDeAmbito.ScopeExists(Ambito.ScopeName))
            {
                this.loguear("El ambito " + Ambito.ScopeName + " ya existe!! Inicio:\t" + DateTime.Now);
            }
            else
            {
                this.loguear("Se va a crear el ambito " + Ambito.ScopeName + " Inicio:\t" + DateTime.Now);

                proveedorDeAmbito.PopulateFromScopeDescription(Ambito);
                proveedorDeAmbito.SetCreateTableDefault(DbSyncCreationOption.CreateOrUseExisting);
                proveedorDeAmbito.Apply();

                this.loguear("Se creo el ambito " + Ambito.ScopeName + " fin:\t" + DateTime.Now);
            }
        }

        private void DesaprovicionarAmbito(SqlConnection conexionSql, DbSyncScopeDescription Ambito, SqlSyncScopeProvisioning proveedorDeAmbito)
        {
            SqlSyncScopeDeprovisioning DesaprovicionadorDeAmbito = new SqlSyncScopeDeprovisioning(conexionSql);
            DesaprovicionadorDeAmbito.ObjectSchema = this.esquemaMetadataSyncFramework;
            DesaprovicionadorDeAmbito.ObjectPrefix = this.prefijoMetadataSyncFramework;
            this.loguear("Se va a eliminar el ambito " + Ambito.ScopeName + " Inicio:\t" + DateTime.Now);
            lock(this.obj)
            {
                if (proveedorDeAmbito.ScopeExists(Ambito.ScopeName))            
                    DesaprovicionadorDeAmbito.DeprovisionScope(Ambito.ScopeName);
            }
            
            this.loguear("Se elimino el ambito " + Ambito.ScopeName + " fin:\t" + DateTime.Now);
            
        }

        private void DesaprovicionarServidor(SqlConnection conexionSql )
        {
            SqlSyncScopeDeprovisioning DesaprovicionadorDeAmbito = new SqlSyncScopeDeprovisioning(conexionSql);
            DesaprovicionadorDeAmbito.ObjectSchema = this.esquemaMetadataSyncFramework;
            DesaprovicionadorDeAmbito.ObjectPrefix = this.prefijoMetadataSyncFramework;
            try
            {
                this.loguear("Se va a desaprovicionar el servidor entero " + conexionSql.Database + " Inicio:\t" + DateTime.Now);
                DesaprovicionadorDeAmbito.DeprovisionStore();
                this.loguear("Se desaprovicio el servidor entero " + conexionSql.Database + " fin:\t" + DateTime.Now);
            }
            catch (Exception e)
            {
                this.loguear(e.Message);
            }
        }

        private SqlSyncScopeProvisioning CrearProveedorDeAmbito(String esquemaMetadataSyncFramework, String prefijoMetadataSyncFramework, SqlConnection conexionSql, DbSyncScopeDescription Ambito)
        {
            SqlSyncScopeProvisioning proveedorDeAmbito = new SqlSyncScopeProvisioning(conexionSql, Ambito);
            proveedorDeAmbito.ObjectSchema = esquemaMetadataSyncFramework;
            proveedorDeAmbito.ObjectPrefix = prefijoMetadataSyncFramework;
            return proveedorDeAmbito;
        }

        private DbSyncScopeDescription CrearAmbito(String nombreDeAmbito, string TablaDentroDelAmbito, SqlConnection conexionSql)
        {
            DbSyncScopeDescription Ambito = new DbSyncScopeDescription(nombreDeAmbito);
            DbSyncTableDescription descripcionDeTabla = SqlSyncDescriptionBuilder.GetDescriptionForTable(TablaDentroDelAmbito, conexionSql);
            Ambito.Tables.Add(descripcionDeTabla);
            return Ambito;
        }

        private SqlSyncProvider ObtenerProveedor(String nombreDeAmbito, SqlConnection conexionSql, uint tamañoDeCache)
        {
            SqlSyncProvider proveedor = new SqlSyncProvider(nombreDeAmbito, conexionSql, this.prefijoMetadataSyncFramework, this.esquemaMetadataSyncFramework);

            if (tamañoDeCache > 0)
                proveedor.MemoryDataCacheSize = tamañoDeCache; //KB --> los archivos de cache se guardan en BatchingDirectory (%tmp% por default)

            proveedor.Connection = conexionSql;

            proveedor.ApplyChangeFailed += new EventHandler<DbApplyChangeFailedEventArgs>(proveedor_ApplyChangeFailed); //Este tiene logica importante, no quitar.

            if (this.SuscribirATodosLosEventos)
            {
                proveedor.BatchApplied += new EventHandler<DbBatchAppliedEventArgs>(Proveedor_BatchApplied);
                proveedor.BatchSpooled += new EventHandler<DbBatchSpooledEventArgs>(Proveedor_BatchSpooled);
                proveedor.ChangesSelected += new EventHandler<DbChangesSelectedEventArgs>(proveedor_ChangesSelected);
                proveedor.ApplyingChanges += new EventHandler<DbApplyingChangesEventArgs>(proveedor_ApplyingChanges);


                proveedor.ApplyMetadataFailed += new EventHandler<ApplyMetadataFailedEventArgs>(proveedor_ApplyMetadataFailed);
                proveedor.ChangesApplied += new EventHandler<DbChangesAppliedEventArgs>(proveedor_ChangesApplied);

                proveedor.DbConnectionFailure += new EventHandler<DbConnectionFailureEventArgs>(proveedor_DbConnectionFailure);
                proveedor.SelectingChanges += new EventHandler<DbSelectingChangesEventArgs>(proveedor_SelectingChanges);
                proveedor.SyncPeerOutdated += new EventHandler<DbOutdatedEventArgs>(proveedor_SyncPeerOutdated);
                proveedor.SyncProgress += new EventHandler<DbSyncProgressEventArgs>(proveedor_SyncProgress);
            }
            return proveedor;
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

        protected void loguearEstadisticas(SyncOperationStatistics estadisticas, DbSyncScopeDescription ambito)
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
            this.loguear( message );
        }

        protected void orchestrator_StateChanged(object sender, SyncOrchestratorStateChangedEventArgs e)
        {
            String mensaje =
                string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r",
                "\tNuevo estado Del Orquestador", e.NewState.ToString(),
                "\tAnterior estado Del Orquestador", e.OldState.ToString()
                );
            //this.loguear("------orchestrator->StateChanged------: \n\r" + mensaje);

        }

        protected void orchestrator_SessionProgress(object sender, SyncStagedProgressEventArgs e)
        {
            String mensaje =
               string.Format(
               "{0}: {1} {2}: {3} {4}: {5} {6}: {7}",
               "Trabajo terminado", e.CompletedWork,
               "\tProveedor que reporta", e.ReportingProvider,
               "\tEstado: ", e.Stage,
               "\tTrabajo total: ", e.TotalWork
               );

            this.loguear( mensaje );
        }

        protected void Proveedor_BatchApplied(object sender, DbBatchAppliedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r",
                "\tDestination Database:", ((RelationalSyncProvider)sender).Connection.Database,
                "\tBatch Number:", e.CurrentBatchNumber,
                "\tTotal Batches To Apply :", e.TotalBatchesToApply);

            this.loguear("------BatchApplied event fired------: \n\r" + mensaje);
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

            this.loguear("------BatchSpooled event fired: Details------: \n\r" + mensaje);

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

            this.loguear("------Proveedor_ChangesSelected: Details------: \n\r" + mensaje);
        }

        protected void proveedor_ApplyingChanges(object sender, DbApplyingChangesEventArgs e)
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

            this.loguear("------proveedor_ApplyingChanges: Details------: \n\r" + mensaje);
        }

        protected void proveedor_ChangesApplied(object sender, DbChangesAppliedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r{6}:{7}\n\r",
                "\tSource Database :", e.Connection.Database,
                "\tContexto        :", e.Context,
                "\tSession Id      :", e.Session.SessionId,
                "\tTransaccion     :", e.Transaction.ToString()
                );

            this.loguear("------proveedor_ChangesApplied: Details------: \n\r" + mensaje);
        }

        protected void proveedor_ApplyChangeFailed(object sender, DbApplyChangeFailedEventArgs e)
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

            this.loguear("------proveedor_ApplyChangeFailed: Details------: \n\r" + mensaje);
        }

        protected void proveedor_ApplyMetadataFailed(object sender, ApplyMetadataFailedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r{6}:{7}\n\r{8}:{9}\n\r",
                "\tSource Database :", e.Connection.Database,
                "\tContexto        :", e.Context,
                "\tError           :", e.Error.Message.ToString(),
                "\tSession Id      :", e.Session.SessionId,
                "\tTransaccion     :", e.Transaction.ToString()
                );

            this.loguear("------proveedor_ApplyMetadataFailed: Details------: \n\r" + mensaje);
        }

        protected void loguear(string mensaje)
        {

            string[] renglones = mensaje.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string renglon in renglones)
            {
                if (this.onLoguearVisualmente != null)
                    this.onLoguearVisualmente(renglon);
                
            }

        }

        private bool ExisteAmbito(string esquemaMetadataSyncFramework, string prefijoMetadataSyncFramework, SqlConnection conexionSql, DbSyncScopeDescription ambito)
        {
            Boolean resultado = false;

            SqlSyncScopeProvisioning serverConfig = new SqlSyncScopeProvisioning( conexionSql );
            serverConfig.ObjectPrefix = prefijoMetadataSyncFramework;
            serverConfig.ObjectSchema = esquemaMetadataSyncFramework;
            resultado = serverConfig.ScopeExists(ambito.ScopeName);
            return resultado;
        }


        protected void proveedor_DbConnectionFailure(object sender, DbConnectionFailureEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}\n\r{4}:{5}\n\r",
                "\tAccion :", e.Action,
                "\tError de conexion:", e.FailureException.Message,
                "\tReintentos       :", e.RetryCount.ToString()
                );

            this.loguear("------proveedor_DbConnectionFailure: Details------: \n\r" + mensaje);        
        }

        protected void proveedor_SelectingChanges(object sender, DbSelectingChangesEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}",
                "\tIsolation Level :", e.Transaction.IsolationLevel,
                "\tTotal actualizados:", e.Context.ScopeProgress.TotalUpdates.ToString()
                );

            this.loguear("------proveedor_SelectingChanges: Details------: \n\r" + mensaje);        
        }

        protected void proveedor_SyncPeerOutdated(object sender, DbOutdatedEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}",
                "\tAccion :", e.Action,
                "\tId session:", e.Session.SessionId.ToString()
                );

            this.loguear("------proveedor_SyncPeerOutdated: Details------: \n\r" + mensaje);        
        }

        protected void proveedor_SyncProgress(object sender, DbSyncProgressEventArgs e)
        {
            String mensaje =
               string.Format(
                "{0}:{1}\n\r{2}:{3}",
                "\tEstado :", e.Stage,
                "\tCantidad de cambios:", e.TableProgress.TotalChanges.ToString()
                );

            this.loguear("------proveedor_SyncProgress: Details------: \n\r" + mensaje);        
        }

        private void ControlarEsquemas(string esquemaQueSeReplica )
        {
            ControladorEsquemas controlador = new ControladorEsquemas();
            if (this.conexionLocalSql.State != System.Data.ConnectionState.Open)
            {
                this.loguear( "Conexion local no esta disponible. Imposible validar esquemas.");
            }
            else
            {
                controlador.Controlar(this.conexionLocalSql, conexionLocalSql.Database, esquemaQueSeReplica );
                controlador.Controlar(this.conexionLocalSql, conexionLocalSql.Database, this.esquemaMetadataSyncFramework);
            }

            if (this.conexionRemotoSql.State != System.Data.ConnectionState.Open)
            {
                this.loguear("Conexion remota no esta disponible. Imposible validar esquemas.");
            }
            else
            {
                controlador.Controlar(this.conexionRemotoSql, conexionRemotoSql.Database, esquemaQueSeReplica );
                controlador.Controlar(this.conexionRemotoSql, conexionRemotoSql.Database, this.esquemaMetadataSyncFramework);
            }
        }

        private void ConectarConSQLServer(string stringConnectionLocal, string stringConnectionRocal)
        {
            //////////////// CONEXION CON SQL SERVER ////////////////
            this.conexionLocalSql = new SqlConnection(stringConnectionLocal);
            this.conexionRemotoSql = new SqlConnection(stringConnectionRocal);

            try
            {
                conexionLocalSql.Open();
            }
            catch (Exception er)
            {
                this.loguear("Error! al abrir conexión local!!: " + er.ToString());
            }

            try
            {
                conexionRemotoSql.Open();
            }
            catch (Exception er)
            {
                this.loguear("Error! al abrir conexión remota!!: " + er.ToString());
            }
        }

        private void InicializarVariablesDeSeteoSync(ParametrosReplica parametros)
        {
            this.prefijoParaNombreDeAmbito = parametros.prefijoParaNombreDeAmbito;
            this.loguear("Prefijo para nombre de Ambito: " + prefijoParaNombreDeAmbito);
            this.esquemaMetadataSyncFramework = parametros.esquemaMetadataSyncFramework;
            this.loguear("Esquema para Metadata de Sync Framework: " + esquemaMetadataSyncFramework);
            this.prefijoMetadataSyncFramework = parametros.prefijoMetadataSyncFramework;
            this.loguear("Prefijo para Metadata de Sync Framework: " + prefijoMetadataSyncFramework);
            this.esquemaQueSeReplica = parametros.esquemaQueSeReplica;
            this.loguear("Esquema de la base que se replica: " + parametros.esquemaQueSeReplica);
            this.SuscribirATodosLosEventos = parametros.SuscribirseATodosLosEventosDeInformacion;
        }

        private void MantenerAprovisionamientoDeAmbitosEnHilos(ParametrosReplica parametros, string esquemaMetadataSyncFramework, string prefijoMetadataSyncFramework, List<DbSyncScopeDescription> Ambitos)
        {
            this.semaforo = new Semaphore(parametros.HilosParaAprovisionar, parametros.HilosParaAprovisionar);
            this.HilosAprovisionando = 0;

            foreach (DbSyncScopeDescription ambito in Ambitos)
            {

                DbSyncScopeDescription LocalAmbito = ambito;

                Thread t = new Thread(delegate()
                {
                    this.MantenerAprovisionamientoDeAmbitos(parametros, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, LocalAmbito);
                });
                t.Start();
            }

            while (this.HilosAprovisionando != 0)
            {
                Thread.Sleep(500);
            }
        }

        private void ReplicarAmbitos(SyncOrchestrator orchestrator, List<DbSyncScopeDescription> Ambitos, ParametrosReplica parametrosReplica ) //int timeOut, uint tamañoDeCache)
        {
            foreach (DbSyncScopeDescription ambito in Ambitos)
            {
                //////////////// PROVEEDORES DE REPLICA (CONOCEN LA LOGICA DEL MOTOR DE DATOS A REPLICAR)  ////////////////
                SqlSyncProvider proveedorLocal = this.ObtenerProveedor(ambito.ScopeName, conexionLocalSql, parametrosReplica.tamañoDeCache);
                SqlSyncProvider proveedorRemoto = this.ObtenerProveedor(ambito.ScopeName, conexionRemotoSql, parametrosReplica.tamañoDeCache);

                proveedorLocal.CommandTimeout = parametrosReplica.TimeOut;
                proveedorRemoto.CommandTimeout = parametrosReplica.TimeOut;

                orchestrator.LocalProvider = proveedorLocal;
                orchestrator.RemoteProvider = proveedorRemoto;
                try
                {
                    bool replicar = true;
                    int anclaActual = this.ObtenerAnclaActual();
                    if (parametrosReplica.ReplicarSoloAmbitosconCambios)
                    {
                        replicar = this.ElAmbitoTieneNovedades( ambito );
                    }
                    if (replicar)
                    {
                        SyncOperationStatistics statsUpload = orchestrator.Synchronize();
                        this.loguearEstadisticas(statsUpload, ambito);
                        this.ActualizarAnclaDeSincronizacion(anclaActual, ambito);
                    }
                }
                catch (Exception error)
                {
                    this.loguear("Error al replicar el ambito: " + ambito.ScopeName + " Error: " + error.ToString());
                }

            }
        }

        private bool ElAmbitoTieneNovedades( DbSyncScopeDescription ambito)
        {
            bool retorno = false;

            string tablaAnclas = this.ObtenerNombreTablaAnclas();
            int anclaDeLaUltimaSincronizacion = this.ObtenerUltimaAnclaDeSincronizacion(ambito);

            string procedimientoAlmacenado = ambito.Tables[0].UnquotedGlobalName + "_selectchanges";
            SqlCommand cmd = new SqlCommand(procedimientoAlmacenado, this.conexionLocalSql);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@sync_min_timestamp", anclaDeLaUltimaSincronizacion));
            cmd.Parameters.Add(new SqlParameter("@sync_scope_local_id", 1));
            cmd.Parameters.Add(new SqlParameter("@sync_scope_restore_count", 1 ));
            cmd.Parameters.Add(new SqlParameter("@sync_update_peer_key", 1));
            
            SqlDataReader rdr = cmd.ExecuteReader();
            retorno = rdr.HasRows;
            rdr.Close();
            if (!retorno)
            {
                this.loguear("El ambito: " + ambito.ScopeName + " no tiene novedades desde el ancla: " + anclaDeLaUltimaSincronizacion);
            }

            return retorno;
        }

        private void ActualizarAnclaDeSincronizacion(int anclaActual, DbSyncScopeDescription ambito)
        {
            string tablaAnclas = this.ObtenerNombreTablaAnclas();

            int ultimaAnclaDeSincronizacion = this.ObtenerUltimaAnclaDeSincronizacion(ambito);
            string comando = "";
            if ( ultimaAnclaDeSincronizacion >= 0)
            {

                comando = "update " + tablaAnclas + " set  last_anchor_sync = " + anclaActual +
                        ", last_sync_datetime = getdate() where sync_scope_name = '" + ambito.ScopeName + "'" +
                         " and sync_remote_DataBase_name = '" + this.nombreDeBaseRemota + "'";
                
            }
            else
            {
                comando = "insert into " + tablaAnclas +
                    " ( sync_scope_name, last_anchor_sync, last_sync_datetime, sync_remote_DataBase_name ) values " +
                    "('" + ambito.ScopeName + "'," + anclaActual.ToString() + ", getdate(), '" + this.nombreDeBaseRemota + "' )";

            }

            using (SqlCommand consulta = new SqlCommand(comando, this.conexionLocalSql))
            {
                consulta.ExecuteNonQuery();
            }
        }

        private int ObtenerUltimaAnclaDeSincronizacion(DbSyncScopeDescription ambito)
        {
            string tablaAnclas = this.ObtenerNombreTablaAnclas();
            string comando = "select last_anchor_sync from " + tablaAnclas +
                " where sync_scope_name = '" + ambito.ScopeName + "'" +
                " and sync_remote_DataBase_name = '" + this.nombreDeBaseRemota + "'";

            int ancla = -1;
            using (SqlCommand consulta = new SqlCommand(comando, this.conexionLocalSql))
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

        private int ObtenerAnclaActual()
        {
            int ancla = 0;
            using (SqlCommand consulta = new SqlCommand("select min_active_rowversion() - 1", this.conexionLocalSql))
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

        private void loguearNivelDeTracingConfigurador()
        {
            if (SyncTracer.IsErrorEnabled() || SyncTracer.IsWarningEnabled() || SyncTracer.IsInfoEnabled() || SyncTracer.IsVerboseEnabled())
            {
                this.loguear("Tracing activado! revisar en config cual es el archivo.");
            }
            else
            {
                this.loguear("Tracing desactivado, activar en el config.");
            }


            if (SyncTracer.IsErrorEnabled())
                this.loguear("Tracing de errores Activado");

            if (SyncTracer.IsWarningEnabled())
                this.loguear("Tracing de advertencias Activado");

            if (SyncTracer.IsInfoEnabled())
                this.loguear("Tracing de información Activado");

            if (SyncTracer.IsVerboseEnabled())
                this.loguear("Tracing de todo Activado");
        }
    }
}



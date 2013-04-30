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
        public delegate void LoguearEventHandler( string renglon );
        public delegate void replicaFinalizada();
        public event LoguearEventHandler onLoguearVisualmente;
        public event replicaFinalizada onProcesoFinalizado;
        public string TiempoTotalTranscurrido { get; set; }
        private Semaphore semaforo;
        private int HilosAprovisionando = 0;
        Stopwatch stopWatch;
        
        public void IniciarReplica( Artefacto artefacto)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            #region Datos necesarios para replicar -- Configuracion --
            string prefijoParaNombreDeAmbito = artefacto.prefijoParaNombreDeAmbito;
            string esquemaMetadataSyncFramework = artefacto.esquemaMetadataSyncFramework ; //CREATE SCHEMA SyncZooLogic;
            string prefijoMetadataSyncFramework = artefacto.prefijoMetadataSyncFramework;
            #endregion

            #region SE CREA CONEXION CON SQL SERVER
            //////////////// CONEXION CON SQL SERVER ////////////////
            SqlConnection conexionLocalSql = new SqlConnection(artefacto.StringConnectionLocal);
            SqlConnection conexionRemotoSql = new SqlConnection(artefacto.StringConnectionRemoto);

            try
            {
                conexionLocalSql.Open();
            }
            catch (Exception er)
            {
                this.loguear("Al inicio da Error al abrir conexión local!!: " + er.ToString());
            }

            try
            {
                conexionRemotoSql.Open();
            }
            catch (Exception er)
            {
                this.loguear("Al inicio da Error al abrir conexión remota!!: " + er.ToString());
            }            
            
            
            #endregion

            #region CONTROLA Y CREACION DE ESQUEMAS EN EN SERVIDOR LOCAL Y REMOTO
            //////////////// CONTROLAMOS ESQUEMAS EN EN SERVIDOR LOCAL Y REMOTO  ////////////////
            ControladorEsquemas.Controlar( conexionLocalSql, conexionLocalSql.Database, "ZooLogic");
            ControladorEsquemas.Controlar( conexionLocalSql, conexionLocalSql.Database, esquemaMetadataSyncFramework);

            ControladorEsquemas.Controlar( conexionRemotoSql, conexionRemotoSql.Database, "ZooLogic");
            ControladorEsquemas.Controlar( conexionRemotoSql, conexionRemotoSql.Database, esquemaMetadataSyncFramework);
            #endregion

            #region Desaprovicionar totalmente un servidor
            /////////////// Desaprovicionar Servidor ///////////////
            if (artefacto.LimpiarServidorLocal)
            {
                this.DesaprovicionarServidor(conexionLocalSql, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework);
            }
            if (artefacto.LimpiarServidorRemoto)
            {                
                this.DesaprovicionarServidor(conexionRemotoSql, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework);
            }
            #endregion

            #region Crear coleccion de Ambitos
            /////////////// AMBITO CON UNA TABLA ///////////////
            List<DbSyncScopeDescription> Ambitos = new List<DbSyncScopeDescription>() ;
            foreach (string tabla in artefacto.ListaDeTablas)
            {
                var tablaSplit = tabla.Split('.');
                string schema = tablaSplit[0];
                string NombreTabla = tablaSplit[1];

                Ambitos.Add(this.CrearAmbito(String.Format(prefijoParaNombreDeAmbito, schema, NombreTabla), tabla, conexionLocalSql));
            }
            #endregion

            #region Se crea proveedor de ambito y se desaproviciona y aproviciona segun corresponda.

            this.semaforo = new Semaphore(artefacto.HilosParaAprovisionar, artefacto.HilosParaAprovisionar);
            this.HilosAprovisionando = 0;

            foreach (DbSyncScopeDescription ambito in Ambitos)
            {
                
                DbSyncScopeDescription LocalAmbito = ambito;
                
                Thread t = new Thread( delegate(){
                    this.MantenerAprovisionamientoDeAmbitos(artefacto, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, conexionLocalSql, conexionRemotoSql, LocalAmbito);
                });
                t.Start();
                
            }

            while ( this.HilosAprovisionando != 0)
            {
                Thread.Sleep(500);
            }

            #endregion  

            #region Orquestador de replica
            //////////////// ORQUESTADOR DE REPLICA ////////////////
            SyncOrchestrator orchestrator = new SyncOrchestrator();
            if (artefacto.SitioDeSubida)
                orchestrator.Direction = SyncDirectionOrder.Upload;
            else
                orchestrator.Direction = SyncDirectionOrder.Download;
            
            orchestrator.SessionProgress += new EventHandler<SyncStagedProgressEventArgs>(orchestrator_SessionProgress);
            orchestrator.StateChanged += new EventHandler<SyncOrchestratorStateChangedEventArgs>(orchestrator_StateChanged);
            #endregion

            #region Se crean los proveedores de replica por cada ambito y se replica el ambito
            foreach (DbSyncScopeDescription ambito in Ambitos)
            {
                //////////////// PROVEEDORES DE REPLICA (CONOCEN LA LOGICA DEL MOTOR DE DATOS A REPLICAR)  ////////////////
                SqlSyncProvider proveedorLocal = this.ObtenerProveedor(ambito.ScopeName, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, conexionLocalSql, artefacto.tamañoDeCache);
                SqlSyncProvider proveedorRemoto = this.ObtenerProveedor(ambito.ScopeName, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, conexionRemotoSql, artefacto.tamañoDeCache);

                proveedorLocal.CommandTimeout = artefacto.TimeOut;
                proveedorRemoto.CommandTimeout = artefacto.TimeOut;

                orchestrator.LocalProvider = proveedorLocal;
                orchestrator.RemoteProvider = proveedorRemoto;
                if (artefacto.RealizarReplica)
                {
                    SyncOperationStatistics statsUpload = orchestrator.Synchronize();
                    this.loguearEstadisticas(statsUpload, ambito);
                }
            }
            #endregion


            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            this.TiempoTotalTranscurrido = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);

            if ( this.onProcesoFinalizado != null )
                this.onProcesoFinalizado();
        }

        private void MantenerAprovisionamientoDeAmbitos(Artefacto artefacto, string esquemaMetadataSyncFramework, string prefijoMetadataSyncFramework, SqlConnection conexionLocalSql, SqlConnection conexionRemotoSql, DbSyncScopeDescription ambito)
        {
            this.semaforo.WaitOne();
            this.HilosAprovisionando++;
            try
            {
                SqlConnection NuevaConexionLocalSql = null;
                SqlConnection NuevaConexionRemotoSql = null;
                try
                {
                    NuevaConexionLocalSql = new SqlConnection(artefacto.StringConnectionLocal);
                    NuevaConexionLocalSql.Open();
                    ///SqlCommand comando = new SqlCommand("SET TRANSACTION ISOLATION LEVEL snapshot", NuevaConexionLocalSql);
                    //comando.ExecuteNonQuery();
                }
                catch (Exception er)
                {
                    this.loguear("Error al abrir conexión local!!: " + er.ToString());
                }

                try
                {
                    NuevaConexionRemotoSql = new SqlConnection(artefacto.StringConnectionRemoto);
                    NuevaConexionRemotoSql.Open();
                    //comando = new SqlCommand("SET TRANSACTION ISOLATION LEVEL snapshot", NuevaConexionRemotoSql);
                    //comando.ExecuteNonQuery();
                }
                catch (Exception er)
                {
                    this.loguear("Error al abrir conexión remota!!: " + er.ToString());
                }



                /////////////// PROVEEDORES DE APROVICIONAMIENTO DE  AMBITOS ///////////////
                SqlSyncScopeProvisioning proveedorDeAmbitoLocal = this.CrearProveedorDeAmbito(esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, NuevaConexionLocalSql, ambito);
                SqlSyncScopeProvisioning proveedorDeAmbitoRemoto = this.CrearProveedorDeAmbito(esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, NuevaConexionRemotoSql, ambito);
                proveedorDeAmbitoLocal.CommandTimeout = artefacto.TimeOut;
                proveedorDeAmbitoRemoto.CommandTimeout = artefacto.TimeOut;

                #region Desaprovicionar Ambitos
                /////////////// DESAPROVICIONAR AMBITOS ///////////////
                if (artefacto.DesaprovisionarAmbitosEnServidorLocal)
                {
                    this.DesaprovicionarAmbito(NuevaConexionLocalSql, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, ambito, proveedorDeAmbitoLocal);
                }
                if (artefacto.DesaprovisionarAmbitosEnServidorRemoto)
                {
                    this.DesaprovicionarAmbito(NuevaConexionRemotoSql, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, ambito, proveedorDeAmbitoRemoto);
                }
                #endregion

                #region Aprovicionar Ambitos
                /////////////// APROVICIONAR AMBITOS ///////////////
                if (artefacto.AprovisionarAmbitosEnServidorLocal)
                {
                    this.AprovicionarAmbito(ambito, proveedorDeAmbitoLocal);
                }
                if (artefacto.AprovisionarAmbitosEnServidorRemoto)
                {
                    this.AprovicionarAmbito(ambito, proveedorDeAmbitoRemoto);
                }
                #endregion


                if (NuevaConexionLocalSql.State == ConnectionState.Open)
                {
                    NuevaConexionLocalSql.Close();
                }

                if (NuevaConexionRemotoSql.State == ConnectionState.Open)
                {
                    NuevaConexionRemotoSql.Close();
                }
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

        private void DesaprovicionarAmbito(SqlConnection conexionSql, string esquemaMetadataSyncFramework, string prefijoMetadataSyncFramework, DbSyncScopeDescription Ambito, SqlSyncScopeProvisioning proveedorDeAmbito)
        {
            SqlSyncScopeDeprovisioning DesaprovicionadorDeAmbito = new SqlSyncScopeDeprovisioning(conexionSql);
            DesaprovicionadorDeAmbito.ObjectSchema = esquemaMetadataSyncFramework;
            DesaprovicionadorDeAmbito.ObjectPrefix = prefijoMetadataSyncFramework;
            this.loguear("Se va a eliminar el ambito " + Ambito.ScopeName + " Inicio:\t" + DateTime.Now);
            lock(this.obj)
            {
                if (proveedorDeAmbito.ScopeExists(Ambito.ScopeName))            
                    DesaprovicionadorDeAmbito.DeprovisionScope(Ambito.ScopeName);
            }
            
            this.loguear("Se elimino el ambito " + Ambito.ScopeName + " fin:\t" + DateTime.Now);
            
        }

        private void DesaprovicionarServidor(SqlConnection conexionSql, string esquemaMetadataSyncFramework, string prefijoMetadataSyncFramework )
        {
            SqlSyncScopeDeprovisioning DesaprovicionadorDeAmbito = new SqlSyncScopeDeprovisioning(conexionSql);
            DesaprovicionadorDeAmbito.ObjectSchema = esquemaMetadataSyncFramework;
            DesaprovicionadorDeAmbito.ObjectPrefix = prefijoMetadataSyncFramework;
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

        private SqlSyncProvider ObtenerProveedor(String nombreDeAmbito, String esquemaMetadataSyncFramework, String prefijoMetadataSyncFramework, SqlConnection conexionSql, uint tamañoDeCache)
        {
            SqlSyncProvider proveedor = new SqlSyncProvider(nombreDeAmbito, conexionSql, prefijoMetadataSyncFramework, esquemaMetadataSyncFramework);

            if (tamañoDeCache > 0)
                proveedor.MemoryDataCacheSize = tamañoDeCache; //KB --> los archivos de cache se guardan en BatchingDirectory (%tmp% por default)

            proveedor.Connection = conexionSql;

            proveedor.BatchApplied += new EventHandler<DbBatchAppliedEventArgs>(Proveedor_BatchApplied);
            proveedor.BatchSpooled += new EventHandler<DbBatchSpooledEventArgs>(Proveedor_BatchSpooled);
            proveedor.ChangesSelected += new EventHandler<DbChangesSelectedEventArgs>(proveedor_ChangesSelected);
            proveedor.ApplyingChanges += new EventHandler<DbApplyingChangesEventArgs>(proveedor_ApplyingChanges);
            proveedor.ApplyChangeFailed += new EventHandler<DbApplyChangeFailedEventArgs>(proveedor_ApplyChangeFailed);
            proveedor.ApplyMetadataFailed += new EventHandler<ApplyMetadataFailedEventArgs>(proveedor_ApplyMetadataFailed);
            proveedor.ChangesApplied += new EventHandler<DbChangesAppliedEventArgs>(proveedor_ChangesApplied);

            proveedor.DbConnectionFailure += new EventHandler<DbConnectionFailureEventArgs>(proveedor_DbConnectionFailure);
            proveedor.SelectingChanges += new EventHandler<DbSelectingChangesEventArgs>(proveedor_SelectingChanges);
            proveedor.SyncPeerOutdated += new EventHandler<DbOutdatedEventArgs>(proveedor_SyncPeerOutdated);
            proveedor.SyncProgress += new EventHandler<DbSyncProgressEventArgs>(proveedor_SyncProgress);

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
    }
}



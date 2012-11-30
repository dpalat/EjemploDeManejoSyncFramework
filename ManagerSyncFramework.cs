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

namespace EjemploDeManejoSyncFramework
{
    public class ManagerSyncFramework
    {
        private System.Windows.Forms.ListBox Logueador;

        public ManagerSyncFramework(System.Windows.Forms.ListBox listBox)
        {
            this.Logueador = listBox;
        }

        public void IniciarReplica( Artefacto artefacto)
        {

            #region Datos necesarios para replicar -- Configuracion --
            String prefijoParaNombreDeAmbito = "Novedades_";
            String esquemaMetadataSyncFramework = "SyncZooLogic"; //CREATE SCHEMA SyncZooLogic;
            String prefijoMetadataSyncFramework = "Sql_Replica";
            #endregion

            #region SE CREA CONEXION CON SQL SERVER
            //////////////// CONEXION CON SQL SERVER ////////////////
            SqlConnection conexionLocalSql = new SqlConnection(artefacto.StringConnectionLocal);
            SqlConnection conexionRemotoSql = new SqlConnection(artefacto.StringConnectionRemoto);
            
            conexionLocalSql.Open();
            conexionRemotoSql.Open();
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
                Ambitos.Add(this.CrearAmbito(prefijoParaNombreDeAmbito + tabla, tabla, conexionLocalSql));
            }
            #endregion

            #region Se crea proveedor de ambito y se desaproviciona y aproviciona segun corresponda.
            foreach (DbSyncScopeDescription ambito in Ambitos)
            {
                /////////////// PROVEEDORES DE APROVICIONAMIENTO DE  AMBITOS ///////////////
                SqlSyncScopeProvisioning proveedorDeAmbitoLocal = this.CrearProveedorDeAmbito(esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, conexionLocalSql, ambito);
                SqlSyncScopeProvisioning proveedorDeAmbitoRemoto = this.CrearProveedorDeAmbito(esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, conexionRemotoSql, ambito);
                //proveedorDeAmbitoLocal.CommandTimeout = 999999999;
                #region Desaprovicionar Ambitos
                /////////////// DESAPROVICIONAR AMBITOS ///////////////
                if (artefacto.DesaprovisionarAmbitosEnServidorLocal)
                {
                    this.DesaprovicionarAmbito(conexionLocalSql, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, ambito, proveedorDeAmbitoLocal);   
                }
                if (artefacto.DesaprovisionarAmbitosEnServidorRemoto)
                {
                    this.DesaprovicionarAmbito(conexionRemotoSql, esquemaMetadataSyncFramework, prefijoMetadataSyncFramework, ambito, proveedorDeAmbitoRemoto);
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

                orchestrator.LocalProvider = proveedorLocal;
                orchestrator.RemoteProvider = proveedorRemoto;
                if (artefacto.RealizarReplica)
                {
                    SyncOperationStatistics statsUpload = orchestrator.Synchronize();
                    this.loguearEstadisticas(statsUpload, ambito);
                }
            }
            #endregion

        }

        private void AprovicionarAmbito(DbSyncScopeDescription Ambito, SqlSyncScopeProvisioning proveedorDeAmbito)
        {
            if (!proveedorDeAmbito.ScopeExists(Ambito.ScopeName))
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
            if (proveedorDeAmbito.ScopeExists(Ambito.ScopeName))
                DesaprovicionadorDeAmbito.DeprovisionScope(Ambito.ScopeName);
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
                this.Logueador.Items.Add(renglon);
            }
            this.Logueador.Refresh();
            this.Logueador.SelectedIndex = this.Logueador.Items.Count - 1;    
            this.Logueador.SelectedIndex = -1;
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



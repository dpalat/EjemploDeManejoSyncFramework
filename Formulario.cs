using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;
using Microsoft.Synchronization.Data;

namespace EjemploDeManejoSyncFramework
{
    public partial class Formulario : Form
    {
        public delegate void Function();
        private ManagerSyncFramework ManagerSync;
        private ParametrosReplica parametrosReplica;

        public Formulario()
        {
            InitializeComponent();
        }

        private void btnIniciar_Click(object sender, EventArgs e)
        {
            
            this.lstLogueo.Items.Clear();
            this.lstLogueo.Refresh();
            this.ManagerSync = new ManagerSyncFramework();
            this.ManagerSync.onLoguearVisualmente += this.loguear;
            this.ManagerSync.onProcesoFinalizado += this.procesoFinalizado;
            this.parametrosReplica = new ParametrosReplica();
            this.parametrosReplica.AprovisionarAmbitosEnServidorLocal = this.chkAprovisionarAmbitosEnServidorLocal.Checked;
            this.parametrosReplica.AprovisionarAmbitosEnServidorRemoto = this.chkAprovisionarAmbitosEnServidorRemoto.Checked;
            this.parametrosReplica.DesaprovisionarAmbitosEnServidorLocal = this.chkDesaprovisionarAmbitosEnServidorLocal.Checked;
            this.parametrosReplica.DesaprovisionarAmbitosEnServidorRemoto = this.chkDesaprovisionarAmbitosEnServidorRemoto.Checked;
            this.parametrosReplica.LimpiarServidorLocal = this.chkLimpiarServidorLocal.Checked;
            this.parametrosReplica.LimpiarServidorRemoto = this.chkLimpiarServidorRemoto.Checked;
            this.parametrosReplica.RealizarReplica = this.chkReplicar.Checked;
            this.parametrosReplica.StringConnectionLocal = this.txtStringConnectionLocal.Text + ";Application Name=Manager de Sync Framework;";
            this.parametrosReplica.StringConnectionRemoto = this.txtStringConnectionRemoto.Text + ";Application Name=Manager de Sync Framework;"; 
            this.parametrosReplica.ListaDeTablas = this.ObtenerListaDeTablas( this.lctChkTablasLocalesAReplicar.CheckedItems );
            this.parametrosReplica.SitioDeSubida = this.chkSitioLocalDeSubida.Checked;
            this.parametrosReplica.tamañoDeCache = Convert.ToUInt32(this.txtTamañoCache.Text.ToString());
            this.parametrosReplica.TamañoDeTransaccion = Convert.ToUInt32(this.txtTamañoTransaccion.Text.ToString());
            this.parametrosReplica.TimeOut = Convert.ToInt32( this.txtTimeOut.Text.ToString());
            this.parametrosReplica.prefijoMetadataSyncFramework = this.txtPrefijoMetadata.Text; // "Sql_Replica";
            this.parametrosReplica.esquemaMetadataSyncFramework = this.txtEsquemaMetadata.Text; // "SyncZooLogic";
            this.parametrosReplica.prefijoParaNombreDeAmbito = this.txtPrefijoAmbitos.Text; //"Novedades_[{0}].[{1}]"; //Novedades_[ZooLogic].[ADT_COMB]
            this.parametrosReplica.esquemaQueSeReplica = this.txtEsquemaAReplicar.Text;
            this.parametrosReplica.HilosParaAprovisionar = Convert.ToInt32( this.txtHilosAprovisionar.Text.ToString() );
            this.parametrosReplica.HilosParaReplicar = Convert.ToInt32( this.txtHilosReplica.Text.ToString() );
            this.parametrosReplica.ReplicarSoloAmbitosconCambios = this.chkSoloConCambios.Checked;
            this.parametrosReplica.SuscribirseATodosLosEventosDeInformacion = this.chkSuscribirseATodos.Checked;
            if (!this.chkSitioLocalDeSubida.Checked && !this.chkSitioLocalDeBajada.Checked)
            {
                System.Windows.Forms.MessageBox.Show("Debe indicar si el sitio es de subida o bajada.");
                return;
            }
                
            try
            {
                this.activarBotones(false);
                System.Threading.Thread nuevoHilo = new System.Threading.Thread(this.IniciarReplica);
                nuevoHilo.Name = "Replicando manager de sincronización";
                nuevoHilo.Start();                
            }
            catch (Exception de)
            {
                this.loguear(de.ToString());
                this.procesoFinalizado();
            }
        }

        private void procesoFinalizado()
        {
            this.activarBotones(true);
            this.loguear(string.Format( "-------Tiempo Total: {0}--------", this.ManagerSync.TiempoTotalTranscurrido) );
            this.loguear("-------Proceso finalizado--------");
        }

        private void activarBotones( bool activados )
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Function(delegate() { this.activarBotones( activados ); }));
            }
            else
            {
                this.btnIniciar.Enabled = activados;
                this.btnCargarTablas.Enabled = activados;
                this.btnSeleccionarNinguno.Enabled = activados;
                this.btnSeleccionarTodos.Enabled = activados;
                this.chkSitioLocalDeBajada.Enabled = activados;
                this.chkSitioLocalDeSubida.Enabled = activados;
            }
        }

        private void IniciarReplica()
        {
            try
            {
                this.ManagerSync.IniciarReplica(this.parametrosReplica);
            }
            catch (Exception ed)
            {
                this.loguear("Full! Error en el proceso de replica: " + ed.ToString());
                this.procesoFinalizado();
            }
        }

        private List<string> ObtenerListaDeTablas(CheckedListBox.CheckedItemCollection objectCollection)
        {
            List<string> retorno = new List<string>();
            foreach ( string item in objectCollection)
            {
                retorno.Add(item);
            }
            return retorno;
        }

        private void btnCargarTablas_Click(object sender, EventArgs e)
        {

            if (!(this.chkSitioLocalDeBajada.Checked || this.chkSitioLocalDeSubida.Checked))
            {
                MessageBox.Show("Se requiere setear el sentido de comunicación SUBIDA o BAJADA");
                return;
            }

            try
            {
                string cadeDeConexion = this.txtStringConnectionLocal.Text;


                if (this.chkSitioLocalDeBajada.Checked)
                {
                    //La estructura sale del sitio que tiene los datos.
                    cadeDeConexion = this.txtStringConnectionRemoto.Text;
                }

                using (SqlConnection conexionLocalSql = new SqlConnection(cadeDeConexion))
                {
                    conexionLocalSql.Open();
                    SqlCommand comando = new SqlCommand("Select TABLE_SCHEMA, TABLE_NAME From INFORMATION_SCHEMA.Tables" +
                        " where table_schema = '" + this.txtEsquemaAReplicar.Text + "' order by 1,2");
                    comando.Connection = conexionLocalSql;
                    SqlDataReader data = comando.ExecuteReader();
                    this.lctChkTablasLocalesAReplicar.Items.Clear();
                    while (data.Read())
                    {
                        this.lctChkTablasLocalesAReplicar.Items.Add(data[0] + "." + data[1]);
                    }
                    this.actulizarMensajesDeCantidadDeTablas();
                }
                
            }
            catch (Exception dde)
            {
                this.loguear("Error al cargar las tablas: " + dde.ToString());
            }
        }

        private void actulizarMensajesDeCantidadDeTablas()
        {
            this.lblCantidadDeTablas.Text = this.lctChkTablasLocalesAReplicar.Items.Count.ToString();
            this.lblCantidadSeleccionadas.Text = this.lctChkTablasLocalesAReplicar.CheckedItems.Count.ToString();
        }

        private void chkSitioLocalDeSubida_CheckedChanged(object sender, EventArgs e)
        {
            this.chkSitioLocalDeBajada.Checked = !this.chkSitioLocalDeSubida.Checked;
        }

        private void chkSitioLocalDeBajada_CheckedChanged(object sender, EventArgs e)
        {
            this.chkSitioLocalDeSubida.Checked = !this.chkSitioLocalDeBajada.Checked;
        }

        private void btnSeleccionarTodos_Click(object sender, EventArgs e)
        {
            for ( int i = 0; i < this.lctChkTablasLocalesAReplicar.Items.Count; i++)
                this.lctChkTablasLocalesAReplicar.SetItemChecked( i, true );

            this.actulizarMensajesDeCantidadDeTablas();
        }

        private void btnSeleccionarNinguno_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < this.lctChkTablasLocalesAReplicar.Items.Count; i++)
                this.lctChkTablasLocalesAReplicar.SetItemChecked(i, false);

            this.actulizarMensajesDeCantidadDeTablas();
        }

        private void lctChkTablasLocalesAReplicar_Click(object sender, EventArgs e)
        {
            this.actulizarMensajesDeCantidadDeTablas();
        }

        private void lctChkTablasLocalesAReplicar_SelectedValueChanged(object sender, EventArgs e)
        {
            this.actulizarMensajesDeCantidadDeTablas();
        }

        private void lctChkTablasLocalesAReplicar_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            this.actulizarMensajesDeCantidadDeTablas();
        }

        private void lctChkTablasLocalesAReplicar_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.actulizarMensajesDeCantidadDeTablas();
        }

        private void lctChkTablasLocalesAReplicar_Validated(object sender, EventArgs e)
        {
            this.actulizarMensajesDeCantidadDeTablas();
        }

        private void loguear( string renglon )
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Function(delegate() { this.loguear(renglon); }));
            }
            else
            {
                this.lstLogueo.Items.Add(renglon);
                this.lstLogueo.Refresh();
                this.lstLogueo.SelectedIndex = this.lstLogueo.Items.Count - 1;
                this.lstLogueo.SelectedIndex = -1;
            }
        }

        private void Formulario_Load(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void txtHilosReplica_TextChanged(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label17_Click(object sender, EventArgs e)
        {

        }
    }
}

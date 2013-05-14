using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace EjemploDeManejoSyncFramework
{
    public partial class Formulario : Form
    {
        public delegate void Function();
        private ManagerSyncFramework ManagerSync;
        private Artefacto artefacto;
        private string esquemaQueSeReplica = "ZooLogic";

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
            this.artefacto = new Artefacto();
            this.artefacto.AprovisionarAmbitosEnServidorLocal = this.chkAprovisionarAmbitosEnServidorLocal.Checked;
            this.artefacto.AprovisionarAmbitosEnServidorRemoto = this.chkAprovisionarAmbitosEnServidorRemoto.Checked;
            this.artefacto.DesaprovisionarAmbitosEnServidorLocal = this.chkDesaprovisionarAmbitosEnServidorLocal.Checked;
            this.artefacto.DesaprovisionarAmbitosEnServidorRemoto = this.chkDesaprovisionarAmbitosEnServidorRemoto.Checked;
            this.artefacto.LimpiarServidorLocal = this.chkLimpiarServidorLocal.Checked;
            this.artefacto.LimpiarServidorRemoto = this.chkLimpiarServidorRemoto.Checked;
            this.artefacto.RealizarReplica = this.chkReplicar.Checked;
            this.artefacto.StringConnectionLocal = this.txtStringConnectionLocal.Text;
            this.artefacto.StringConnectionRemoto = this.txtStringConnectionRemoto.Text;
            this.artefacto.ListaDeTablas = this.ObtenerListaDeTablas( this.lctChkTablasLocalesAReplicar.CheckedItems );
            this.artefacto.SitioDeSubida = this.chkSitioLocalDeSubida.Checked;
            this.artefacto.tamañoDeCache = Convert.ToUInt32(this.txtTamañoCache.Text.ToString());
            this.artefacto.TimeOut = Convert.ToInt32( this.txtTimeOut.Text.ToString());
            this.artefacto.prefijoMetadataSyncFramework = "Sql_Replica";
            this.artefacto.esquemaMetadataSyncFramework = "SyncZooLogic";
            this.artefacto.prefijoParaNombreDeAmbito = "Novedades_[{0}].[{1}]"; //Novedades_[ZooLogic].[ADT_COMB]
            this.artefacto.esquemaQueSeReplica = this.esquemaQueSeReplica;
            this.artefacto.HilosParaAprovisionar = Convert.ToInt32( this.txtHilosAprovisionar.Text.ToString() );
            this.artefacto.HilosParaReplicar = Convert.ToInt32( this.txtHilosReplica.Text.ToString() );

            if (!this.chkSitioLocalDeSubida.Checked && !this.chkSitioLocalDeBajada.Checked)
            {
                System.Windows.Forms.MessageBox.Show("Debe indicar si el sitio es de subida o bajada.");
                return;
            }
                
            try
            {
                this.activarBotones(false);
                System.Threading.Thread nuevoHilo = new System.Threading.Thread(this.IniciarReplica);
                nuevoHilo.Name = "Replicando manager de sincronizacion";
                nuevoHilo.Start();                
            }
            catch (Exception de)
            {
                this.lstLogueo.Items.Add(de.Message);
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
                this.ManagerSync.IniciarReplica(this.artefacto);
            }
            catch (Exception ed)
            {
                this.loguear("Full! Error en el proceso de replica: " + ed.ToString());
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
            try
            {
                using (SqlConnection conexionLocalSql = new SqlConnection(this.txtStringConnectionLocal.Text))
                {
                    conexionLocalSql.Open();
                    SqlCommand comando = new SqlCommand("Select TABLE_SCHEMA, TABLE_NAME From [DRAGONFISH_DEMO].INFORMATION_SCHEMA.Tables" +
                        " where table_schema = '" + this.esquemaQueSeReplica + "' order by 1,2");
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
    }
}

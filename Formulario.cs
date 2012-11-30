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
        public Formulario()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.lstLogueo.Items.Clear();
            this.lstLogueo.Refresh();
            ManagerSyncFramework ManagerSync = new ManagerSyncFramework(this.lstLogueo);
            Artefacto artefacto = new Artefacto();
            artefacto.AprovisionarAmbitosEnServidorLocal = this.chkAprovisionarAmbitosEnServidorLocal.Checked;
            artefacto.AprovisionarAmbitosEnServidorRemoto = this.chkAprovisionarAmbitosEnServidorRemoto.Checked;
            artefacto.DesaprovisionarAmbitosEnServidorLocal = this.chkDesaprovisionarAmbitosEnServidorLocal.Checked;
            artefacto.DesaprovisionarAmbitosEnServidorRemoto = this.chkDesaprovisionarAmbitosEnServidorRemoto.Checked;
            artefacto.LimpiarServidorLocal = this.chkLimpiarServidorLocal.Checked;
            artefacto.LimpiarServidorRemoto = this.chkLimpiarServidorRemoto.Checked;
            artefacto.RealizarReplica = this.chkReplicar.Checked;
            artefacto.StringConnectionLocal = this.txtStringConnectionLocal.Text;
            artefacto.StringConnectionRemoto = this.txtStringConnectionRemoto.Text;
            artefacto.ListaDeTablas = this.ObtenerListaDeTablas( this.lctChkTablasLocalesAReplicar.CheckedItems );
            artefacto.SitioDeSubida = this.chkSitioLocalDeSubida.Checked;
            artefacto.tamañoDeCache = Convert.ToUInt32(this.txtTamañoCache.Text.ToString());
            try
            {
                ManagerSync.IniciarReplica(artefacto);
            }
            catch (Exception de)
            {
                this.lstLogueo.Items.Add(de.Message);
            }
            finally
            {
                this.lstLogueo.Items.Add("Proceso finalizado.");
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

        private void Formulario_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (SqlConnection conexionLocalSql = new SqlConnection(this.txtStringConnectionLocal.Text))
            {                
                conexionLocalSql.Open();
                SqlCommand comando = new SqlCommand("Select TABLE_SCHEMA, TABLE_NAME From [DRAGONFISH_DEMO].INFORMATION_SCHEMA.Tables order by 1,2");
                comando.Connection = conexionLocalSql;
                SqlDataReader data = comando.ExecuteReader();
                this.lctChkTablasLocalesAReplicar.Items.Clear();
                while (data.Read())
                {
                    this.lctChkTablasLocalesAReplicar.Items.Add(data[0] + "." + data[1]);
                }
                
            }
        }

        private void chkSitioLocalDeSubida_CheckedChanged(object sender, EventArgs e)
        {
            this.chkSitioLocalDeBajada.Checked = !this.chkSitioLocalDeSubida.Checked;
        }

        private void chkSitioLocalDeBajada_CheckedChanged(object sender, EventArgs e)
        {
            this.chkSitioLocalDeSubida.Checked = !this.chkSitioLocalDeBajada.Checked;
        }
    }
}

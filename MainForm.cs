using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace EjemploDeManejoSyncFramework
{
    public partial class MainForm : Form
    {
        private ManagerSyncFramework _managerSync;
        private ParametrosReplica _parametrosReplica;
        
        public MainForm()
        {
            InitializeComponent();
            RecoveryStringConnections();
        }

        public delegate void Function();

        private void ActivarBotones(bool activados)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Function(delegate () { this.ActivarBotones(activados); }));
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

        private void actulizarMensajesDeCantidadDeTablas(bool mostranMensajeDeCantidad)
        {
            this.ActulizarMensajesDeCantidadDeTablas();
            if (mostranMensajeDeCantidad && this.lctChkTablasLocalesAReplicar.Items.Count < 1)
            {
                string orgine = "local";
                if (this.chkSitioLocalDeBajada.Checked)
                {
                    orgine = "remoto";
                }

                MessageBox.Show("No se encontraron tablas del esquema " + this.txtEsquemaAReplicar.Text + " en el " + orgine);
            }
        }

        private void ActulizarMensajesDeCantidadDeTablas()
        {
            this.lblCantidadDeTablas.Text = this.lctChkTablasLocalesAReplicar.Items.Count.ToString();
            this.lblCantidadSeleccionadas.Text = this.lctChkTablasLocalesAReplicar.CheckedItems.Count.ToString();
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
                    this.actulizarMensajesDeCantidadDeTablas(true);
                }
                SaveStringConnections();
            }
            catch (Exception dde)
            {
                this.Loguear("Error al cargar las tablas: " + dde.ToString());
            }
        }

        private void btnIniciar_Click(object sender, EventArgs e)
        {
            SaveStringConnections();
            lstLogueo.Items.Clear();
            lstLogueo.Refresh();
            _managerSync = new ManagerSyncFramework();
            _managerSync.OnLoguearVisualmente += Loguear;
            _managerSync.OnProcesoFinalizado += procesoFinalizado;
            CargarParametros();

            if (!this.chkSitioLocalDeSubida.Checked && !this.chkSitioLocalDeBajada.Checked)
            {
                MessageBox.Show("Debe indicar si el sitio es de subida o bajada.");
                return;
            }

            if (!chkUsarDescripcionLocal.Checked && !chkUsarDescripcionRemota.Checked)
            {
                MessageBox.Show("Debe indicar si utiliza la descripción de ámbitos Local o Remota.");
                return;
            }

            try
            {
                ActivarBotones(false);
                System.Threading.Thread nuevoHilo = new System.Threading.Thread(this.IniciarReplica);
                nuevoHilo.Name = "Replicando manager de sincronización";
                nuevoHilo.Start();
            }
            catch (Exception de)
            {
                this.Loguear(de.ToString());
                this.procesoFinalizado();
            }
        }

        private void SaveStringConnections()
        {
            File.Delete("stringConnectionLocal.ini");
            File.Delete("stringConnectionRemoto.ini");
            File.AppendAllText("stringConnectionLocal.ini", txtStringConnectionLocal.Text);
            File.AppendAllText("stringConnectionRemoto.ini", txtStringConnectionRemoto.Text); 
        }

        private void RecoveryStringConnections()
        {
            if (File.Exists("stringConnectionLocal.ini"))
            {
                try
                {
                    txtStringConnectionLocal.Text = File.ReadAllText("stringConnectionLocal.ini");
                }
                catch (Exception)
                {
                    txtStringConnectionLocal.Text = "";
                }
            }


            if (File.Exists("stringConnectionRemoto.ini"))
            {
                try
                {
                    txtStringConnectionRemoto.Text = File.ReadAllText("stringConnectionRemoto.ini");
                }
                catch (Exception)
                {
                    txtStringConnectionRemoto.Text = "";
                }
            }

            var stingDefault = @"Data Source=.\SQLServer;Initial Catalog=DB_NAME;Integrated Security=False;uid=remoteUID; pwd=1234;";
            if (string.IsNullOrEmpty(txtStringConnectionLocal.Text)) txtStringConnectionLocal.Text = stingDefault;
            if (string.IsNullOrEmpty(txtStringConnectionRemoto.Text)) txtStringConnectionRemoto.Text = stingDefault;
        }

        private void btnSeleccionarNinguno_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < this.lctChkTablasLocalesAReplicar.Items.Count; i++)
                this.lctChkTablasLocalesAReplicar.SetItemChecked(i, false);

            this.ActulizarMensajesDeCantidadDeTablas();
        }

        private void btnSeleccionarTodos_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < this.lctChkTablasLocalesAReplicar.Items.Count; i++)
                this.lctChkTablasLocalesAReplicar.SetItemChecked(i, true);

            this.ActulizarMensajesDeCantidadDeTablas();
        }

        private void btnSerializarAmbitos_Click(object sender, EventArgs e)
        {
            CargarParametros();
            _managerSync = new ManagerSyncFramework();
            _managerSync.OnLoguearVisualmente += Loguear;
            _managerSync.OnProcesoFinalizado += procesoFinalizado;

            var json = _managerSync.ObtenerAmbitosSerializados(_parametrosReplica);
            Clipboard.SetText(json);
            Loguear($@"Ambitos >>> JSON >>> Clipboard {Environment.NewLine}{Environment.NewLine}Hecho!");
        }

        private void CargarParametros()
        {
            _parametrosReplica = new ParametrosReplica
            {
                AprovisionarAmbitosEnServidorLocal = this.chkAprovisionarAmbitosEnServidorLocal.Checked,
                AprovisionarAmbitosEnServidorRemoto = this.chkAprovisionarAmbitosEnServidorRemoto.Checked,
                DesaprovisionarAmbitosEnServidorLocal = this.chkDesaprovisionarAmbitosEnServidorLocal.Checked,
                DesaprovisionarAmbitosEnServidorRemoto = this.chkDesaprovisionarAmbitosEnServidorRemoto.Checked,
                LimpiarServidorLocal = this.chkLimpiarServidorLocal.Checked,
                LimpiarServidorRemoto = this.chkLimpiarServidorRemoto.Checked,
                RealizarReplica = this.chkReplicar.Checked,
                StringConnectionLocal =
                    this.txtStringConnectionLocal.Text + ";Application Name=Manager de Sync Framework;",
                StringConnectionRemoto =
                    this.txtStringConnectionRemoto.Text + ";Application Name=Manager de Sync Framework;",
                 
                ListaDeTablas = this.ObtenerListaDeTablas(this.lctChkTablasLocalesAReplicar.CheckedItems),
                SitioDeSubida = this.chkSitioLocalDeSubida.Checked,
                TamañoDeCache = Convert.ToUInt32(this.txtTamañoCache.Text.ToString()),
                TamañoDeTransaccion = Convert.ToUInt32(this.txtTamañoTransaccion.Text.ToString()),
                TimeOut = Convert.ToInt32(this.txtTimeOut.Text.ToString()),
                prefijoMetadataSyncFramework = this.txtPrefijoMetadata.Text,
                esquemaMetadataSyncFramework = this.txtEsquemaMetadata.Text,
                prefijoParaNombreDeAmbito = this.txtPrefijoAmbitos.Text,
                esquemaQueSeReplica = this.txtEsquemaAReplicar.Text,
                HilosParaAprovisionar = Convert.ToInt32(this.txtHilosAprovisionar.Text.ToString()),
                HilosParaReplicar = Convert.ToInt32(this.txtHilosReplica.Text.ToString()),
                ReplicarSoloAmbitosconCambios = this.chkSoloConCambios.Checked,
                SuscribirseATodosLosEventosDeInformacion = this.chkSuscribirseATodos.Checked,
                UsarDescripcionLocal = chkUsarDescripcionLocal.Checked,
                UsarDescripcionRemota = chkUsarDescripcionRemota.Checked
            };
        }

        private void chkSitioLocalDeBajada_CheckedChanged(object sender, EventArgs e)
        {
            this.chkSitioLocalDeSubida.Checked = !this.chkSitioLocalDeBajada.Checked;
        }

        private void chkSitioLocalDeSubida_CheckedChanged(object sender, EventArgs e)
        {
            this.chkSitioLocalDeBajada.Checked = !this.chkSitioLocalDeSubida.Checked;
        }

        private void chkUsarDescripcionLocal_CheckedChanged(object sender, EventArgs e)
        {
            chkUsarDescripcionRemota.Checked = !chkUsarDescripcionLocal.Checked;
        }

        private void chkUsarDescripcionRemota_CheckedChanged(object sender, EventArgs e)
        {
            chkUsarDescripcionLocal.Checked = !chkUsarDescripcionRemota.Checked;
        }

        private void Formulario_Load(object sender, EventArgs e)
        {        
            var listboxContextMenu = new ContextMenuStrip();
            listboxContextMenu.Items.Add("Copiar contenido en el porta papeles").Click += ContextMenuClick_Logs;
            listboxContextMenu.Items.Add("Vaciar").Click += ContextMenuClick_Clean;
            lstLogueo.ContextMenuStrip = listboxContextMenu;

            listboxContextMenu = new ContextMenuStrip();
            listboxContextMenu.Items.Add("Copiar contenido en el porta papeles").Click += ContextMenuClick_Tables;
            lctChkTablasLocalesAReplicar.ContextMenuStrip = listboxContextMenu;
        }

        private void ContextMenuClick_Clean(object sender, EventArgs e)
        {
            lstLogueo.Items.Clear();
        }

        private void ContextMenuClick_Logs(object sender, EventArgs e)
        {
            ContextMenuClick(lstLogueo);
        }

        private void ContextMenuClick_Tables(object sender, EventArgs e)
        {
            ContextMenuClick(lctChkTablasLocalesAReplicar);
        }

        private void ContextMenuClick(ListBox listbox)
        {
            var fullLog = string.Empty;
            foreach (var item in listbox.Items)
            {
                fullLog += item + "\r\n";
            }
            if (string.IsNullOrEmpty(fullLog)) return;
            Clipboard.SetText(fullLog);
        }


        private void IniciarReplica()
        {
            try
            {
                this._managerSync.IniciarReplica(this._parametrosReplica);
            }
            catch (Exception ed)
            {
                this.Loguear("Full! Error en el proceso de replica: " + ed.ToString());
                this.procesoFinalizado();
            }
        }

        private void lctChkTablasLocalesAReplicar_Click(object sender, EventArgs e)
        {
            this.ActulizarMensajesDeCantidadDeTablas();
        }

        private void lctChkTablasLocalesAReplicar_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            this.ActulizarMensajesDeCantidadDeTablas();
        }

        private void lctChkTablasLocalesAReplicar_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.ActulizarMensajesDeCantidadDeTablas();
        }

        private void lctChkTablasLocalesAReplicar_SelectedValueChanged(object sender, EventArgs e)
        {
            ActulizarMensajesDeCantidadDeTablas();
        }

        private void lctChkTablasLocalesAReplicar_Validated(object sender, EventArgs e)
        {
            ActulizarMensajesDeCantidadDeTablas();
        }

        private void Loguear(string renglon)
        {
            if (InvokeRequired)
            {
                Invoke(new Function(delegate () { Loguear(renglon); }));
            }
            else
            {
                string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff",
                                            CultureInfo.InvariantCulture);
                lstLogueo.Items.Add(timestamp + " - " + renglon);
                lstLogueo.Refresh();
                lstLogueo.SelectedIndex = lstLogueo.Items.Count - 1;
                lstLogueo.SelectedIndex = -1;
            }
        }

        private List<string> ObtenerListaDeTablas(CheckedListBox.CheckedItemCollection objectCollection)
        {
            List<string> retorno = new List<string>();
            foreach (string item in objectCollection)
            {
                retorno.Add(item);
            }
            return retorno;
                
        }

        private void procesoFinalizado()
        {
            this.ActivarBotones(true);
            this.Loguear(string.Format("-------Tiempo Total: {0}--------", this._managerSync.TiempoTotalTranscurrido));
            this.Loguear("-------Proceso finalizado--------");
        }
        private void txtHilosReplica_TextChanged(object sender, EventArgs e)
        {
        }

        private void pictureBox15_Click(object sender, EventArgs e)
        {
            var strLocal = txtStringConnectionLocal.Text.Trim();
            var strRemoto = txtStringConnectionRemoto.Text.Trim();

            txtStringConnectionRemoto.Text = strLocal;
            txtStringConnectionLocal.Text = strRemoto;
        }
    }
}
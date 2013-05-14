namespace EjemploDeManejoSyncFramework
{
    partial class Formulario
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Formulario));
            this.btnIniciar = new System.Windows.Forms.Button();
            this.lstLogueo = new System.Windows.Forms.ListBox();
            this.chkLimpiarServidorLocal = new System.Windows.Forms.CheckBox();
            this.chkLimpiarServidorRemoto = new System.Windows.Forms.CheckBox();
            this.chkDesaprovisionarAmbitosEnServidorLocal = new System.Windows.Forms.CheckBox();
            this.chkDesaprovisionarAmbitosEnServidorRemoto = new System.Windows.Forms.CheckBox();
            this.chkAprovisionarAmbitosEnServidorLocal = new System.Windows.Forms.CheckBox();
            this.chkAprovisionarAmbitosEnServidorRemoto = new System.Windows.Forms.CheckBox();
            this.chkReplicar = new System.Windows.Forms.CheckBox();
            this.txtStringConnectionLocal = new System.Windows.Forms.TextBox();
            this.txtStringConnectionRemoto = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.lctChkTablasLocalesAReplicar = new System.Windows.Forms.CheckedListBox();
            this.btnCargarTablas = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.chkSitioLocalDeBajada = new System.Windows.Forms.CheckBox();
            this.chkSitioLocalDeSubida = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtTamañoCache = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnSeleccionarTodos = new System.Windows.Forms.Button();
            this.btnSeleccionarNinguno = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.txtTimeOut = new System.Windows.Forms.TextBox();
            this.lblMensajeCantidad = new System.Windows.Forms.Label();
            this.lblCantidadDeTablas = new System.Windows.Forms.Label();
            this.lblCantidadSeleccionadas = new System.Windows.Forms.Label();
            this.lblMensajeSeleccionadas = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.txtHilosAprovisionar = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.txtHilosReplica = new System.Windows.Forms.TextBox();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnIniciar
            // 
            this.btnIniciar.Location = new System.Drawing.Point(26, 10);
            this.btnIniciar.Name = "btnIniciar";
            this.btnIniciar.Size = new System.Drawing.Size(881, 39);
            this.btnIniciar.TabIndex = 0;
            this.btnIniciar.Text = "Iniciar";
            this.btnIniciar.UseVisualStyleBackColor = true;
            this.btnIniciar.Click += new System.EventHandler(this.btnIniciar_Click);
            // 
            // lstLogueo
            // 
            this.lstLogueo.FormattingEnabled = true;
            this.lstLogueo.HorizontalScrollbar = true;
            this.lstLogueo.Location = new System.Drawing.Point(240, 263);
            this.lstLogueo.Name = "lstLogueo";
            this.lstLogueo.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lstLogueo.Size = new System.Drawing.Size(667, 368);
            this.lstLogueo.TabIndex = 1;
            // 
            // chkLimpiarServidorLocal
            // 
            this.chkLimpiarServidorLocal.AutoSize = true;
            this.chkLimpiarServidorLocal.Location = new System.Drawing.Point(26, 55);
            this.chkLimpiarServidorLocal.Name = "chkLimpiarServidorLocal";
            this.chkLimpiarServidorLocal.Size = new System.Drawing.Size(124, 17);
            this.chkLimpiarServidorLocal.TabIndex = 2;
            this.chkLimpiarServidorLocal.Text = "Limpiar servidor local";
            this.chkLimpiarServidorLocal.UseVisualStyleBackColor = true;
            // 
            // chkLimpiarServidorRemoto
            // 
            this.chkLimpiarServidorRemoto.AutoSize = true;
            this.chkLimpiarServidorRemoto.Location = new System.Drawing.Point(26, 78);
            this.chkLimpiarServidorRemoto.Name = "chkLimpiarServidorRemoto";
            this.chkLimpiarServidorRemoto.Size = new System.Drawing.Size(134, 17);
            this.chkLimpiarServidorRemoto.TabIndex = 3;
            this.chkLimpiarServidorRemoto.Text = "Limpiar servidor remoto";
            this.chkLimpiarServidorRemoto.UseVisualStyleBackColor = true;
            // 
            // chkDesaprovisionarAmbitosEnServidorLocal
            // 
            this.chkDesaprovisionarAmbitosEnServidorLocal.AutoSize = true;
            this.chkDesaprovisionarAmbitosEnServidorLocal.Location = new System.Drawing.Point(186, 55);
            this.chkDesaprovisionarAmbitosEnServidorLocal.Name = "chkDesaprovisionarAmbitosEnServidorLocal";
            this.chkDesaprovisionarAmbitosEnServidorLocal.Size = new System.Drawing.Size(228, 17);
            this.chkDesaprovisionarAmbitosEnServidorLocal.TabIndex = 4;
            this.chkDesaprovisionarAmbitosEnServidorLocal.Text = "Desaprovisionar Ambitos en Servidor Local";
            this.chkDesaprovisionarAmbitosEnServidorLocal.UseVisualStyleBackColor = true;
            // 
            // chkDesaprovisionarAmbitosEnServidorRemoto
            // 
            this.chkDesaprovisionarAmbitosEnServidorRemoto.AutoSize = true;
            this.chkDesaprovisionarAmbitosEnServidorRemoto.Location = new System.Drawing.Point(186, 78);
            this.chkDesaprovisionarAmbitosEnServidorRemoto.Name = "chkDesaprovisionarAmbitosEnServidorRemoto";
            this.chkDesaprovisionarAmbitosEnServidorRemoto.Size = new System.Drawing.Size(239, 17);
            this.chkDesaprovisionarAmbitosEnServidorRemoto.TabIndex = 5;
            this.chkDesaprovisionarAmbitosEnServidorRemoto.Text = "Desaprovisionar Ambitos en Servidor Remoto";
            this.chkDesaprovisionarAmbitosEnServidorRemoto.UseVisualStyleBackColor = true;
            // 
            // chkAprovisionarAmbitosEnServidorLocal
            // 
            this.chkAprovisionarAmbitosEnServidorLocal.AutoSize = true;
            this.chkAprovisionarAmbitosEnServidorLocal.Location = new System.Drawing.Point(441, 55);
            this.chkAprovisionarAmbitosEnServidorLocal.Name = "chkAprovisionarAmbitosEnServidorLocal";
            this.chkAprovisionarAmbitosEnServidorLocal.Size = new System.Drawing.Size(210, 17);
            this.chkAprovisionarAmbitosEnServidorLocal.TabIndex = 6;
            this.chkAprovisionarAmbitosEnServidorLocal.Text = "Aprovisionar Ambitos en Servidor Local";
            this.chkAprovisionarAmbitosEnServidorLocal.UseVisualStyleBackColor = true;
            // 
            // chkAprovisionarAmbitosEnServidorRemoto
            // 
            this.chkAprovisionarAmbitosEnServidorRemoto.AutoSize = true;
            this.chkAprovisionarAmbitosEnServidorRemoto.Location = new System.Drawing.Point(441, 78);
            this.chkAprovisionarAmbitosEnServidorRemoto.Name = "chkAprovisionarAmbitosEnServidorRemoto";
            this.chkAprovisionarAmbitosEnServidorRemoto.Size = new System.Drawing.Size(221, 17);
            this.chkAprovisionarAmbitosEnServidorRemoto.TabIndex = 7;
            this.chkAprovisionarAmbitosEnServidorRemoto.Text = "Aprovisionar Ambitos en Servidor Remoto";
            this.chkAprovisionarAmbitosEnServidorRemoto.UseVisualStyleBackColor = true;
            // 
            // chkReplicar
            // 
            this.chkReplicar.AutoSize = true;
            this.chkReplicar.Location = new System.Drawing.Point(690, 55);
            this.chkReplicar.Name = "chkReplicar";
            this.chkReplicar.Size = new System.Drawing.Size(65, 17);
            this.chkReplicar.TabIndex = 8;
            this.chkReplicar.Text = "Replicar";
            this.chkReplicar.UseVisualStyleBackColor = true;
            // 
            // txtStringConnectionLocal
            // 
            this.txtStringConnectionLocal.Location = new System.Drawing.Point(159, 116);
            this.txtStringConnectionLocal.Name = "txtStringConnectionLocal";
            this.txtStringConnectionLocal.Size = new System.Drawing.Size(637, 20);
            this.txtStringConnectionLocal.TabIndex = 9;
            this.txtStringConnectionLocal.Text = "Data Source=.\\SQLEXPRESS;Initial Catalog=DEMO_DEMO;Integrated Security=True;Conne" +
    "ct Timeout=1";
            // 
            // txtStringConnectionRemoto
            // 
            this.txtStringConnectionRemoto.Location = new System.Drawing.Point(159, 152);
            this.txtStringConnectionRemoto.Name = "txtStringConnectionRemoto";
            this.txtStringConnectionRemoto.Size = new System.Drawing.Size(748, 20);
            this.txtStringConnectionRemoto.TabIndex = 10;
            this.txtStringConnectionRemoto.Text = "Data Source=.;Initial Catalog=HUB-PRUEBAfr;User ID=remoto;Password=1";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(23, 119);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(119, 13);
            this.label1.TabIndex = 11;
            this.label1.Text = "String connection Local";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(23, 155);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(130, 13);
            this.label2.TabIndex = 12;
            this.label2.Text = "String connection Remoto";
            // 
            // lctChkTablasLocalesAReplicar
            // 
            this.lctChkTablasLocalesAReplicar.FormattingEnabled = true;
            this.lctChkTablasLocalesAReplicar.Items.AddRange(new object[] {
            "DEBE CARGAR TABLAS"});
            this.lctChkTablasLocalesAReplicar.Location = new System.Drawing.Point(26, 263);
            this.lctChkTablasLocalesAReplicar.Name = "lctChkTablasLocalesAReplicar";
            this.lctChkTablasLocalesAReplicar.Size = new System.Drawing.Size(205, 334);
            this.lctChkTablasLocalesAReplicar.TabIndex = 13;
            this.lctChkTablasLocalesAReplicar.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.lctChkTablasLocalesAReplicar_ItemCheck);
            this.lctChkTablasLocalesAReplicar.Click += new System.EventHandler(this.lctChkTablasLocalesAReplicar_Click);
            this.lctChkTablasLocalesAReplicar.SelectedIndexChanged += new System.EventHandler(this.lctChkTablasLocalesAReplicar_SelectedIndexChanged);
            this.lctChkTablasLocalesAReplicar.SelectedValueChanged += new System.EventHandler(this.lctChkTablasLocalesAReplicar_SelectedValueChanged);
            this.lctChkTablasLocalesAReplicar.Validated += new System.EventHandler(this.lctChkTablasLocalesAReplicar_Validated);
            // 
            // btnCargarTablas
            // 
            this.btnCargarTablas.Location = new System.Drawing.Point(26, 194);
            this.btnCargarTablas.Name = "btnCargarTablas";
            this.btnCargarTablas.Size = new System.Drawing.Size(205, 33);
            this.btnCargarTablas.TabIndex = 14;
            this.btnCargarTablas.Text = "Cargar tablas";
            this.btnCargarTablas.UseVisualStyleBackColor = true;
            this.btnCargarTablas.Click += new System.EventHandler(this.btnCargarTablas_Click);
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.chkSitioLocalDeBajada);
            this.panel1.Controls.Add(this.chkSitioLocalDeSubida);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Location = new System.Drawing.Point(802, 55);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(105, 81);
            this.panel1.TabIndex = 15;
            // 
            // chkSitioLocalDeBajada
            // 
            this.chkSitioLocalDeBajada.AutoSize = true;
            this.chkSitioLocalDeBajada.Location = new System.Drawing.Point(5, 58);
            this.chkSitioLocalDeBajada.Name = "chkSitioLocalDeBajada";
            this.chkSitioLocalDeBajada.Size = new System.Drawing.Size(59, 17);
            this.chkSitioLocalDeBajada.TabIndex = 2;
            this.chkSitioLocalDeBajada.Text = "Bajada";
            this.chkSitioLocalDeBajada.UseVisualStyleBackColor = true;
            this.chkSitioLocalDeBajada.CheckedChanged += new System.EventHandler(this.chkSitioLocalDeBajada_CheckedChanged);
            // 
            // chkSitioLocalDeSubida
            // 
            this.chkSitioLocalDeSubida.AutoSize = true;
            this.chkSitioLocalDeSubida.Location = new System.Drawing.Point(5, 35);
            this.chkSitioLocalDeSubida.Name = "chkSitioLocalDeSubida";
            this.chkSitioLocalDeSubida.Size = new System.Drawing.Size(59, 17);
            this.chkSitioLocalDeSubida.TabIndex = 1;
            this.chkSitioLocalDeSubida.Text = "Subida";
            this.chkSitioLocalDeSubida.UseVisualStyleBackColor = true;
            this.chkSitioLocalDeSubida.CheckedChanged += new System.EventHandler(this.chkSitioLocalDeSubida_CheckedChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(0, 7);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(104, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Base de datos Local";
            // 
            // txtTamañoCache
            // 
            this.txtTamañoCache.AccessibleRole = System.Windows.Forms.AccessibleRole.SplitButton;
            this.txtTamañoCache.Location = new System.Drawing.Point(724, 71);
            this.txtTamañoCache.Name = "txtTamañoCache";
            this.txtTamañoCache.Size = new System.Drawing.Size(71, 20);
            this.txtTamañoCache.TabIndex = 16;
            this.txtTamañoCache.Text = "0";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(670, 74);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(49, 13);
            this.label4.TabIndex = 17;
            this.label4.Text = "Batching";
            // 
            // btnSeleccionarTodos
            // 
            this.btnSeleccionarTodos.Location = new System.Drawing.Point(26, 234);
            this.btnSeleccionarTodos.Name = "btnSeleccionarTodos";
            this.btnSeleccionarTodos.Size = new System.Drawing.Size(100, 23);
            this.btnSeleccionarTodos.TabIndex = 18;
            this.btnSeleccionarTodos.Text = "TODOS";
            this.btnSeleccionarTodos.UseVisualStyleBackColor = true;
            this.btnSeleccionarTodos.Click += new System.EventHandler(this.btnSeleccionarTodos_Click);
            // 
            // btnSeleccionarNinguno
            // 
            this.btnSeleccionarNinguno.Location = new System.Drawing.Point(132, 233);
            this.btnSeleccionarNinguno.Name = "btnSeleccionarNinguno";
            this.btnSeleccionarNinguno.Size = new System.Drawing.Size(99, 23);
            this.btnSeleccionarNinguno.TabIndex = 19;
            this.btnSeleccionarNinguno.Text = "NINGUNO";
            this.btnSeleccionarNinguno.UseVisualStyleBackColor = true;
            this.btnSeleccionarNinguno.Click += new System.EventHandler(this.btnSeleccionarNinguno_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(671, 97);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(47, 13);
            this.label5.TabIndex = 21;
            this.label5.Text = "TimeOut";
            // 
            // txtTimeOut
            // 
            this.txtTimeOut.AccessibleRole = System.Windows.Forms.AccessibleRole.SplitButton;
            this.txtTimeOut.Location = new System.Drawing.Point(724, 94);
            this.txtTimeOut.Name = "txtTimeOut";
            this.txtTimeOut.Size = new System.Drawing.Size(71, 20);
            this.txtTimeOut.TabIndex = 20;
            this.txtTimeOut.Text = "30";
            // 
            // lblMensajeCantidad
            // 
            this.lblMensajeCantidad.AutoSize = true;
            this.lblMensajeCantidad.Location = new System.Drawing.Point(23, 600);
            this.lblMensajeCantidad.Name = "lblMensajeCantidad";
            this.lblMensajeCantidad.Size = new System.Drawing.Size(98, 13);
            this.lblMensajeCantidad.TabIndex = 22;
            this.lblMensajeCantidad.Text = "Cantidad de tablas:";
            // 
            // lblCantidadDeTablas
            // 
            this.lblCantidadDeTablas.AutoSize = true;
            this.lblCantidadDeTablas.Location = new System.Drawing.Point(124, 600);
            this.lblCantidadDeTablas.Name = "lblCantidadDeTablas";
            this.lblCantidadDeTablas.Size = new System.Drawing.Size(13, 13);
            this.lblCantidadDeTablas.TabIndex = 23;
            this.lblCantidadDeTablas.Text = "0";
            // 
            // lblCantidadSeleccionadas
            // 
            this.lblCantidadSeleccionadas.AutoSize = true;
            this.lblCantidadSeleccionadas.Location = new System.Drawing.Point(124, 614);
            this.lblCantidadSeleccionadas.Name = "lblCantidadSeleccionadas";
            this.lblCantidadSeleccionadas.Size = new System.Drawing.Size(13, 13);
            this.lblCantidadSeleccionadas.TabIndex = 25;
            this.lblCantidadSeleccionadas.Text = "0";
            // 
            // lblMensajeSeleccionadas
            // 
            this.lblMensajeSeleccionadas.AutoSize = true;
            this.lblMensajeSeleccionadas.Location = new System.Drawing.Point(23, 614);
            this.lblMensajeSeleccionadas.Name = "lblMensajeSeleccionadas";
            this.lblMensajeSeleccionadas.Size = new System.Drawing.Size(80, 13);
            this.lblMensajeSeleccionadas.TabIndex = 24;
            this.lblMensajeSeleccionadas.Text = "Seleccionadas:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(243, 197);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(115, 13);
            this.label6.TabIndex = 27;
            this.label6.Text = "Hilos para Aprovisionar";
            // 
            // txtHilosAprovisionar
            // 
            this.txtHilosAprovisionar.AccessibleRole = System.Windows.Forms.AccessibleRole.SplitButton;
            this.txtHilosAprovisionar.Enabled = false;
            this.txtHilosAprovisionar.Location = new System.Drawing.Point(367, 194);
            this.txtHilosAprovisionar.Name = "txtHilosAprovisionar";
            this.txtHilosAprovisionar.Size = new System.Drawing.Size(71, 20);
            this.txtHilosAprovisionar.TabIndex = 26;
            this.txtHilosAprovisionar.Text = "1";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(243, 223);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(96, 13);
            this.label7.TabIndex = 29;
            this.label7.Text = "Hilos para Replicar";
            // 
            // txtHilosReplica
            // 
            this.txtHilosReplica.AccessibleRole = System.Windows.Forms.AccessibleRole.SplitButton;
            this.txtHilosReplica.Enabled = false;
            this.txtHilosReplica.Location = new System.Drawing.Point(367, 220);
            this.txtHilosReplica.Name = "txtHilosReplica";
            this.txtHilosReplica.Size = new System.Drawing.Size(71, 20);
            this.txtHilosReplica.TabIndex = 28;
            this.txtHilosReplica.Text = "1";
            // 
            // Formulario
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(919, 652);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.txtHilosReplica);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.txtHilosAprovisionar);
            this.Controls.Add(this.lblCantidadSeleccionadas);
            this.Controls.Add(this.lblMensajeSeleccionadas);
            this.Controls.Add(this.lblCantidadDeTablas);
            this.Controls.Add(this.lblMensajeCantidad);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.txtTimeOut);
            this.Controls.Add(this.btnSeleccionarNinguno);
            this.Controls.Add(this.btnSeleccionarTodos);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtTamañoCache);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.btnCargarTablas);
            this.Controls.Add(this.lstLogueo);
            this.Controls.Add(this.lctChkTablasLocalesAReplicar);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtStringConnectionRemoto);
            this.Controls.Add(this.txtStringConnectionLocal);
            this.Controls.Add(this.chkReplicar);
            this.Controls.Add(this.chkAprovisionarAmbitosEnServidorRemoto);
            this.Controls.Add(this.chkAprovisionarAmbitosEnServidorLocal);
            this.Controls.Add(this.chkDesaprovisionarAmbitosEnServidorRemoto);
            this.Controls.Add(this.chkDesaprovisionarAmbitosEnServidorLocal);
            this.Controls.Add(this.chkLimpiarServidorRemoto);
            this.Controls.Add(this.chkLimpiarServidorLocal);
            this.Controls.Add(this.btnIniciar);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Formulario";
            this.Text = " ";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnIniciar;
        private System.Windows.Forms.ListBox lstLogueo;
        private System.Windows.Forms.CheckBox chkLimpiarServidorLocal;
        private System.Windows.Forms.CheckBox chkLimpiarServidorRemoto;
        private System.Windows.Forms.CheckBox chkDesaprovisionarAmbitosEnServidorLocal;
        private System.Windows.Forms.CheckBox chkDesaprovisionarAmbitosEnServidorRemoto;
        private System.Windows.Forms.CheckBox chkAprovisionarAmbitosEnServidorLocal;
        private System.Windows.Forms.CheckBox chkAprovisionarAmbitosEnServidorRemoto;
        private System.Windows.Forms.CheckBox chkReplicar;
        private System.Windows.Forms.TextBox txtStringConnectionLocal;
        private System.Windows.Forms.TextBox txtStringConnectionRemoto;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckedListBox lctChkTablasLocalesAReplicar;
        private System.Windows.Forms.Button btnCargarTablas;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.CheckBox chkSitioLocalDeBajada;
        private System.Windows.Forms.CheckBox chkSitioLocalDeSubida;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtTamañoCache;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnSeleccionarTodos;
        private System.Windows.Forms.Button btnSeleccionarNinguno;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txtTimeOut;
        private System.Windows.Forms.Label lblMensajeCantidad;
        private System.Windows.Forms.Label lblCantidadDeTablas;
        private System.Windows.Forms.Label lblCantidadSeleccionadas;
        private System.Windows.Forms.Label lblMensajeSeleccionadas;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txtHilosAprovisionar;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox txtHilosReplica;
    }
}


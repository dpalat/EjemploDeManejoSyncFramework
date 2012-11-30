using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EjemploDeManejoSyncFramework
{
    public class Artefacto
    {
        public bool LimpiarServidorRemoto { get; set; }
        public bool LimpiarServidorLocal { get; set; }
        public bool DesaprovisionarAmbitosEnServidorLocal { get; set; }
        public bool DesaprovisionarAmbitosEnServidorRemoto { get; set; }
        public bool AprovisionarAmbitosEnServidorLocal { get; set; }
        public bool AprovisionarAmbitosEnServidorRemoto { get; set; }
        public bool RealizarReplica { get; set; }

        public string StringConnectionLocal { get; set; }
        public string StringConnectionRemoto { get; set; }

        public List<string> ListaDeTablas { get; set; }

        public bool SitioDeSubida { get; set; }

        public uint tamañoDeCache { get; set; }
    }
}

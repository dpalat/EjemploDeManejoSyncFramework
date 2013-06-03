using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;

namespace EjemploDeManejoSyncFramework
{
    public class ControladorEsquemas
    {
        public void Controlar(SqlConnection conexionSql, string baseDeDatos, string esquema)
        {
            if ( conexionSql.State != System.Data.ConnectionState.Open )
                throw new Exception( "No se puede validar esquemas con una conexion que no esta abierta o disponible." );

            using (SqlCommand consultaEsquema = new SqlCommand("select name from [" + baseDeDatos + "].sys.schemas where name='" + esquema+"'", conexionSql))
            {
                using (SqlDataReader readerEsquema = consultaEsquema.ExecuteReader())
                {
                    if (!readerEsquema.HasRows)
                    {
                        readerEsquema.Close();
                        using (SqlCommand CrearEsquema = new SqlCommand("create schema " + esquema, conexionSql)) 
                        {
                            CrearEsquema.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Globals
{
    class Globals
    {
        //public static string sServerLinx = "MTS\\MTS";
        //public static string sConnSQL = "Data Source=" + sServerLinx + ";Initial Catalog=SysFleet;User ID=Ekh_DB_User;Password=Mt267h3#5!S@55wqf;";
        public static string sConnSQL = System.Configuration.ConfigurationManager.ConnectionStrings["SMSVMA.DLL.Properties.Settings.SMSVMAConnectionString"].ConnectionString;
        public static string SelectedHorse;

        public static string UserID;
        public static decimal ThirdPartyFee;


        public static DataTable GetTable(string sCommand)
        {
            SqlConnection SqlCon = new SqlConnection(sConnSQL);

            // Dim Connection As String = "Data Source=" & sServerLinx & ";Initial Catalog=linxdbDIMS;User ID=Ekh_DB_User;Password=gR0!1248$;"

            try
            {
                Console.WriteLine("sConnSQLinx : " + sConnSQL);
                SqlCon.ConnectionString = sConnSQL;
                SqlCon.Open();
            }
            catch (Exception ex)
            {
                // MsgBox("Error Connecting to SQL Server - " & ex.Message, vbCritical)
                return new DataTable();
            }
            SqlCommand cmd = new SqlCommand();
            DataTable dt = new DataTable();
            SqlDataAdapter adapter = new SqlDataAdapter();
            cmd.Connection = SqlCon;
            cmd.CommandText = sCommand;
            cmd.CommandTimeout = 60000;
            adapter.SelectCommand = cmd;
            try
            {
                adapter.Fill(dt);
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                //MM todo throw exception

            }

            adapter.Dispose();
            cmd.Dispose();

            SqlCon.Close();

            return dt;
        }


    }
}

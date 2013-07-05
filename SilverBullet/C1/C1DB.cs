using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;

namespace Linnarsson.C1
{
    public class C1DB
    {
        private readonly static string connectionString = "server=192.168.1.12;uid=cuser;pwd=3pmknHQyl;database=c1;Connect Timeout=300;";

        public C1DB()
        {
        }

        private bool IssueNonQuery(string sql)
        {
            bool success = true;
            MySqlConnection conn = new MySqlConnection(connectionString);
            try
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}: {1}", DateTime.Now, ex);
                success = false;
            }
            conn.Close();
            return success;
        }

        public List<string> GetAllPlateIds()
        {
            List<string> plateIds = new List<string>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            string sql = "SELECT DISTINCT plateid FROM cell";
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string file = rdr["plateid"].ToString();
                plateIds.Add(file);
            }
            rdr.Close();
            conn.Close();
            return plateIds;
        }

        public void InsertCell(Cell c)
        {
            string sql = "INSERT INTO cell () VALUES ()";
            IssueNonQuery(sql);
        }
    }
}

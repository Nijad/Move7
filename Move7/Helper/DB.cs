using MySqlConnector;
using System.Data;

namespace Move7.Helper
{
    internal class DB
    {
        string connectionString;
        MySqlConnection con;
        public DB()
        {
            string server = "127.0.0.1";
            string database = "movedb";
            string username = "moveuser";
            string password = "Move@123";

            connectionString = $"Server={server};Database={database};UID={username};Password={password};SslMode=None";
        }

        private void OpenDB()
        {
            con = new MySqlConnection(connectionString);
            con.Open();
        }

        private void CloseDB()
        {
            con.Close();
        }

        private void ExecuteNonQuery(string query)
        {
            OpenDB();
            MySqlCommand cmd = new MySqlCommand(query, con);
            cmd.ExecuteNonQuery();
            CloseDB();
        }

        private DataTable ExecuteReader(string query)
        {
            OpenDB();
            MySqlCommand cmd = new MySqlCommand(query, con);
            MySqlDataReader dr = cmd.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(dr);
            CloseDB();
            return dt;
        }

        private MySqlCommand ExecuteTransaction(string query)
        {
            OpenDB();
            MySqlCommand cmd = new MySqlCommand(query, con);
            MySqlTransaction transaction;
            transaction = con.BeginTransaction();
            cmd.Transaction = transaction;
            cmd.ExecuteNonQuery();
            return cmd;
        }

        public void Commit(MySqlCommand cmd)
        {
            cmd.Transaction.Commit();
            CloseDB();
        }

        public void Rollback(MySqlCommand cmd)
        {
            cmd.Transaction.Rollback();
            CloseDB();
        }

        public void CheckDatabaseConnection()
        {
            try
            {
                OpenDB();
                CloseDB();
            }
            catch (Exception ex)
            {
                //write in log here
                string msg = "Can not connect database.";
                Logging.WriteNotes(msg);
                Logging.SendEmail(ex, msg);
                Logging.LogException(ex);
                throw ex;
            }
        }

        public void CreateTableIfNotExist()
        {
            try
            {
                string query = "CREATE TABLE IF NOT EXISTS MovedFiles (name varchar(200), " +
                    "Ext varchar(10), RealExt varchar(10), size integer, Dept varchar(50), " +
                    "Destination varchar(50), MoveDate DATETIME)";
                ExecuteNonQuery(query);
            }
            catch (Exception ex)
            {
                string msg = "Can not create table on database";
                Logging.SendEmail(ex, msg);
                Logging.LogException(ex);
                Logging.WriteNotes(msg);
                throw ex;
            }
        }

        public MySqlCommand InsertIntoTable(string name, string extension, string realExtension,
            long size, string dept, string destination)
        {
            string escapedValue = MySqlHelper.EscapeString(name);
            try
            {
                string query = "INSERT INTO `movedfiles`(`name`, `Ext`, `RealExt`, `size`, `Dept`, `Destination`) " +
                    $"VALUES ('{escapedValue}', '{extension}', '{realExtension}', '{size}', '{dept}', '{destination}')";
                MySqlCommand cmd = ExecuteTransaction(query);
                return cmd;
            }
            catch (Exception ex)
            {
                string msg = "Can not write in database";
                Logging.SendEmail(ex, msg);
                Logging.LogException(ex);
            }
            return null;
        }


        public DataTable GetMoveData()
        {
            string query = "select d.dept, (select group_concat(ee.ext) ext from department dd, " +
                "extension ee, dept_ext xx where dd.dept=xx.dept and xx.ext = ee.ext and ee.enabled = 1 " +
                "and (xx.direction = 1 or xx.direction = 3) and d.dept = dd.dept group by dd.dept) ext_in, " +
                "(select group_concat(ee.ext) ext from department dd, extension ee, dept_ext xx " +
                "where dd.dept=xx.dept and xx.ext = ee.ext and ee.enabled = 1 " +
                "and (xx.direction = 2 or xx.direction = 3) and d.dept =dd.dept group by dd.dept) ext_out, " +
                "d.local_path, d.net_path from department d where d.enabled = 1 " +
                "and (select group_concat(ee.ext) ext from department dd, extension ee, dept_ext xx " +
                "where dd.dept=xx.dept and xx.ext = ee.ext and ee.enabled = 1 and " +
                "(xx.direction = 1 or xx.direction = 3) and d.dept = dd.dept group by dd.dept) is not null " +
                "or (select group_concat(ee.ext) ext from department dd, extension ee, dept_ext xx " +
                "where dd.dept=xx.dept and xx.ext = ee.ext and ee.enabled = 1 and " +
                "(xx.direction = 2 or xx.direction = 3) and d.dept = dd.dept group by dd.dept) is not null " +
                "order by d.dept";
             return ExecuteReader(query);
        }

        public DataTable GetConfig() {
            string query = "select `key`, value from config";
            return ExecuteReader(query);
        }
    }
}

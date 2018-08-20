using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace SqliteUtils.Utils
{
    public class SqliteDatabaseManager
    {
        private readonly string _dbFilePath;
        private readonly string _connectionString;
        private readonly object _dbLocker = new object();
        private readonly string _tableVersionName = "table_version";
        private bool _isInit = false;

        public SqliteDatabaseManager(string dbFilePath)
        {
            _dbFilePath = dbFilePath;
            _connectionString = string.Format("data source = {0}", dbFilePath);
        }



        public void Initialize()
        {
            lock (_dbLocker)
            {
                if (_isInit) return;


                _isInit = true;
            }
        }

        private void CreateDbDirIfNotExists(string dbFilePath)
        {
            lock (_dbLocker)
            {
                if (File.Exists(dbFilePath)) return;

                string dir = Path.GetDirectoryName(_dbFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        private int CreateTableVersionIfNotExists()
        {
            lock (_dbLocker)
            {
                string sql = string.Format("CREATE TABLE IF NOT EXISTS {0}(", _tableVersionName) +
                   "table_name varchar(100) PRIMARY KEY," +
                   "version INTEGER)";

                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        cmd.CommandText = sql;
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}

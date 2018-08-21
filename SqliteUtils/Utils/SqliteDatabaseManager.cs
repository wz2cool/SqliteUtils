using Newtonsoft.Json;
using SqliteUtils.Models;
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
            _dbFilePath = Path.GetFullPath(dbFilePath);
            _connectionString = string.Format("data source = \"{0}\"", _dbFilePath);
        }

        public void Initialize()
        {
            lock (_dbLocker)
            {
                if (_isInit) return;
                CreateDatabaseIfNotExists(_dbFilePath);
                CreateTableVersionIfNotExists();
                _isInit = true;
            }
        }

        public int GetTableVersion(string tableName)
        {
            lock (_dbLocker)
            {
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        cmd.CommandText = string.Format("SELECT version FROM {0} WHERE table_name = @tableName", _tableVersionName);
                        cmd.Parameters.AddWithValue("tableName", tableName);
                        var value = cmd.ExecuteScalar();
                        return Convert.ToInt32(value);
                    }
                }
            }
        }

        public string QueryJson(SqlTemplate sqlTemplate)
        {
            lock (_dbLocker)
            {
                var data = QueryData(sqlTemplate);
                return JsonConvert.SerializeObject(data);
            }
        }

        public int ExecuteNonQuery(SqlTemplate sqlTemplate)
        {
            lock (_dbLocker)
            {
                SqliteSqlTemplate wrapper = GetSqliteSqlTemplate(sqlTemplate);
                string sqlExpression = wrapper.SqlExpression;
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(sqlExpression, conn))
                    {
                        if (wrapper.Params != null && wrapper.Params.Length > 0)
                        {
                            cmd.Parameters.AddRange(wrapper.Params.ToArray());
                        }
                        int effectRows = cmd.ExecuteNonQuery();
                        return effectRows;
                    }
                }
            }
        }

        public object ExecuteScalar(SqlTemplate sqlTemplate)
        {
            lock (_dbLocker)
            {
                SqliteSqlTemplate wrapper = GetSqliteSqlTemplate(sqlTemplate);
                string sqlExpression = wrapper.SqlExpression;
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(sqlExpression, conn))
                    {
                        if (wrapper.Params != null && wrapper.Params.Length > 0)
                        {
                            cmd.Parameters.AddRange(wrapper.Params.ToArray());
                        }
                        return cmd.ExecuteScalar();
                    }
                }
            }
        }

        public int ExecuteDML(IEnumerable<SqlTemplate> sqlTemplates)
        {
            lock (_dbLocker)
            {
                IEnumerable<SqliteSqlTemplate> wrapper = GetSqliteSqlTemplates(sqlTemplates);
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            int effectRows = 0;
                            foreach (var item in wrapper)
                            {
                                cmd.CommandText = item.SqlExpression;
                                cmd.Parameters.Clear();
                                if (item.Params != null && item.Params.Length > 0)
                                {
                                    cmd.Parameters.AddRange(item.Params.ToArray());
                                }
                                effectRows += cmd.ExecuteNonQuery();
                            }
                            transaction.Commit();
                            return effectRows;
                        }
                        catch (Exception e)
                        {
                            transaction.Rollback();
                            throw e;
                        }
                    }
                }
            }
        }

        public IEnumerable<Dictionary<string, object>> QueryData(SqlTemplate sqlTemplate)
        {
            lock (_dbLocker)
            {
                var wrapper = GetSqliteSqlTemplate(sqlTemplate);
                string sqlExpression = wrapper.SqlExpression;
                List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(sqlExpression, conn))
                    {
                        if (wrapper.Params != null && wrapper.Params.Length > 0)
                        {
                            cmd.Parameters.AddRange(wrapper.Params.ToArray());
                        }
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                Dictionary<string, object> objDict = new Dictionary<string, object>();
                                for (int i = 0; i < rdr.FieldCount; i++)
                                {
                                    objDict.Add(rdr.GetName(i), rdr.GetValue(i));
                                }
                                result.Add(objDict);
                            }
                        }
                    }
                }
                return result;
            }
        }

        public int CreateTableIfNotExists(CreateTableTemplate createTableTemplate)
        {
            lock (_dbLocker)
            {
                string tableName = createTableTemplate.TableName;
                int version = createTableTemplate.Version;
                string createSql = createTableTemplate.CreateSql;
                int dbTableVersion = GetTableVersion(tableName);
                if (version > dbTableVersion)
                {
                    int effectRows = 0;
                    SqlTemplate createSqlTemplate = new SqlTemplate();
                    createSqlTemplate.SqlExpression = createTableTemplate.CreateSql;
                    effectRows += DropTableIfExists(tableName);
                    effectRows += ExecuteNonQuery(createSqlTemplate);
                    effectRows += UpdateTableVersion(tableName, version);
                    return effectRows;
                }
                else
                {
                    return 0;
                }
            }
        }

        private IEnumerable<SqliteSqlTemplate> GetSqliteSqlTemplates(IEnumerable<SqlTemplate> sqlTemplates)
        {
            List<SqliteSqlTemplate> result = new List<SqliteSqlTemplate>();
            if (sqlTemplates == null || sqlTemplates.Count() == 0)
            {
                return result;
            }

            foreach (var item in sqlTemplates)
            {
                var sqliteSqlTemplate = GetSqliteSqlTemplate(item);
                if (sqliteSqlTemplate != null)
                {
                    result.Add(sqliteSqlTemplate);
                }
            }

            return result;
        }

        private SqliteSqlTemplate GetSqliteSqlTemplate(SqlTemplate sqlTemplate)
        {
            if (sqlTemplate == null || string.IsNullOrWhiteSpace(sqlTemplate.SqlExpression))
            {
                // no need to execute value
                return null;
            }

            SqliteSqlTemplate result = new SqliteSqlTemplate();
            result.SqlExpression = sqlTemplate.SqlExpression;
            List<SQLiteParameter> newParams = new List<SQLiteParameter>();

            if (string.IsNullOrWhiteSpace(sqlTemplate.SqlExpression) || sqlTemplate.Params == null || sqlTemplate.Params.Length == 0)
            {
                return result;
            }

            string sqlExpression = sqlTemplate.SqlExpression;
            String[] sqlPieces = sqlExpression.Split('?');
            sqlExpression = string.Join("", sqlPieces.Select((d, i) => d + (i == sqlPieces.Length - 1 ? "" : "{" + i + "}")));

            for (int i = 0; i < sqlTemplate.Params.Length; i++)
            {
                object value = sqlTemplate.Params[i];
                string name = string.Format("p{0}", i);
                SQLiteParameter sQLiteParameter = new SQLiteParameter(name, value);
                newParams.Add(sQLiteParameter);
                sqlExpression = sqlExpression.Replace("{" + i + "}", "@" + name);
            }

            result.SqlExpression = sqlExpression;
            result.Params = newParams.ToArray();
            return result;
        }


        private void CreateDatabaseIfNotExists(string dbFilePath)
        {
            lock (_dbLocker)
            {
                if (File.Exists(dbFilePath)) return;

                string dir = Path.GetDirectoryName(dbFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                SQLiteConnection.CreateFile(dbFilePath);
            }
        }

        private int CreateTableVersionIfNotExists()
        {
            string sql = string.Format("CREATE TABLE IF NOT EXISTS {0}(", _tableVersionName) +
               "table_name varchar(100) PRIMARY KEY," +
               "version INTEGER)";

            SqlTemplate sqlTemplate = new SqlTemplate();
            sqlTemplate.SqlExpression = sql;
            return ExecuteNonQuery(sqlTemplate);

        }

        private int DropTableIfExists(string tableName)
        {
            string sql = string.Format("DROP TABLE IF EXISTS {0}", tableName);
            SqlTemplate sqlTemplate = new SqlTemplate();
            sqlTemplate.SqlExpression = sql;
            return ExecuteNonQuery(sqlTemplate);
        }

        private int UpdateTableVersion(string tableName, int version)
        {
            string sql = string.Format("INSERT OR REPLACE INTO {0}(table_name, version) VALUES(?, ?)",
                _tableVersionName);
            SqlTemplate sqlTemplate = new SqlTemplate();
            sqlTemplate.SqlExpression = sql;
            sqlTemplate.Params = new object[] { tableName, version };
            return ExecuteDML(new SqlTemplate[] { sqlTemplate });
        }
    }
}

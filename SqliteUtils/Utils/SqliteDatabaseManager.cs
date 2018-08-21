﻿using Newtonsoft.Json;
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
            _dbFilePath = dbFilePath;
            _connectionString = string.Format("data source = {0}", dbFilePath);
        }

        public void Initialize()
        {
            lock (_dbLocker)
            {
                if (_isInit) return;
                CreateDatabaseDirIfNotExists(_dbFilePath);
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
                        cmd.Parameters.AddRange(wrapper.Params.ToArray());
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

        private SqliteSqlTemplate GetSqliteSqlTemplate(SqlTemplate sqlTemplate)
        {
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


        private void CreateDatabaseDirIfNotExists(string dbFilePath)
        {
            lock (_dbLocker)
            {
                if (File.Exists(dbFilePath)) return;

                string dir = Path.GetDirectoryName(dbFilePath);
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

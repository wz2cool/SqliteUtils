using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqliteUtils.Models;
using SqliteUtils.Utils;
using System.Linq;
using System.Collections.Generic;

namespace SqliteUnitTestProject
{
    [TestClass]
    public class SqliteDatabaseManagerUnitTest
    {
        private readonly SqliteDatabaseManager _manager;
        private readonly string dbFile = "test.db";

        public SqliteDatabaseManagerUnitTest()
        {
            if (File.Exists(dbFile))
            {
                File.Delete(dbFile);
            }

            _manager = new SqliteDatabaseManager("test.db");
        }

        [TestMethod]
        public void TestGetSqliteSqlTemplate()
        {
            PrivateObject obj = new PrivateObject(_manager);

            SqlTemplate sqlTemplate = new SqlTemplate();
            sqlTemplate.SqlExpression = "SELECT * FROM users WHERE age = ? AND name = ?";
            sqlTemplate.Params = new object[] { 20, "Frank" };
            var retVal = obj.Invoke("GetSqliteSqlTemplate", sqlTemplate) as SqliteSqlTemplate;
            string expectedSqlExpression = "SELECT * FROM users WHERE age = @p0 AND name = @p1";

            Assert.AreEqual(expectedSqlExpression, retVal.SqlExpression);

            for (int i = 0; i < retVal.Params.Length; i++)
            {
                var param = retVal.Params[i];
                Assert.AreEqual("p" + i, param.ParameterName);
                Assert.AreEqual(sqlTemplate.Params[i], param.Value);
            }
        }

        [TestMethod]
        public void TestCreateTableVersionIfNotExists()
        {
            string dbFilepath = "my.db";
            DeleteDatabase(dbFilepath);
            try
            {
                var manager = new SqliteDatabaseManager(dbFilepath);
                PrivateObject obj = new PrivateObject(manager);
                var r1 = obj.Invoke("CreateDatabaseIfNotExists", Path.GetFullPath(dbFilepath));
                var r2 = obj.Invoke("CreateTableVersionIfNotExists");
                Assert.AreEqual(true, IsTableExists(manager, "table_version"));
            }
            finally
            {
                DeleteDatabase(dbFilepath);
            }
        }

        [TestMethod]
        public void TestUpdateTableVersion()
        {
            string dbFilepath = "my.db";
            DeleteDatabase(dbFilepath);
            try
            {
                var manager = new SqliteDatabaseManager(dbFilepath);
                manager.Initialize();
                PrivateObject obj = new PrivateObject(manager);
                obj.Invoke("UpdateTableVersion", "test", 99);
                var version = manager.GetTableVersion("test");
                Assert.AreEqual(99, version);
            }
            finally
            {
                DeleteDatabase(dbFilepath);
            }
        }

        [TestMethod]
        public void TestCreateTableIfNotExists()
        {
            string dbFilepath = "my.db";
            DeleteDatabase(dbFilepath);
            try
            {
                var manager = new SqliteDatabaseManager(dbFilepath);
                manager.Initialize();

                CreateTableTemplate createTableTemplate = new CreateTableTemplate();
                createTableTemplate.TableName = "student";
                createTableTemplate.Version = 1;
                createTableTemplate.CreateSql = string.Format("CREATE TABLE IF NOT EXISTS {0}(", "student") +
                    "name varchar(100) PRIMARY KEY," +
                    "age INTEGER)";

                PrivateObject obj = new PrivateObject(manager);
                obj.Invoke("CreateTableIfNotExists", createTableTemplate);
                var version = manager.GetTableVersion("student");
                Assert.AreEqual(1, version);

                createTableTemplate.CreateSql = string.Format("CREATE TABLE IF NOT EXISTS {0}(", "student") +
                    "name varchar(100) PRIMARY KEY," +
                    "age INTEGER," +
                    "classroom varchar(100))";
                createTableTemplate.Version = 2;
                obj.Invoke("CreateTableIfNotExists", createTableTemplate);
                version = manager.GetTableVersion("student");
                Assert.AreEqual(2, version);
            }
            finally
            {
                DeleteDatabase(dbFilepath);
            }
        }

        [TestMethod]
        public void TestBulkInserts()
        {
            string dbFilepath = "my.db";
            DeleteDatabase(dbFilepath);
            try
            {
                var manager = new SqliteDatabaseManager(dbFilepath);
                manager.Initialize();
                CreateTableTemplate createTableTemplate = new CreateTableTemplate();
                createTableTemplate.TableName = "student";
                createTableTemplate.Version = 1;
                createTableTemplate.CreateSql = string.Format("CREATE TABLE IF NOT EXISTS {0}(", "student") +
                    "name varchar(100) PRIMARY KEY," +
                    "age INTEGER)";

                PrivateObject obj = new PrivateObject(manager);
                obj.Invoke("CreateTableIfNotExists", createTableTemplate);
                var version = manager.GetTableVersion("student");
                Assert.AreEqual(1, version);

                List<SqlTemplate> sqlTemplates = new List<SqlTemplate>();
                for (int i = 0; i < 1000; i++)
                {
                    SqlTemplate sqlTemplate = new SqlTemplate();
                    string sql = string.Format("INSERT INTO student values(?, ?)");
                    sqlTemplate.SqlExpression = sql;
                    sqlTemplate.Params = new object[] { "student" + i, 20 };
                    sqlTemplates.Add(sqlTemplate);
                }

                int result = manager.ExecuteDML(sqlTemplates);
                Assert.AreEqual(1000, result);
            }
            finally
            {
                DeleteDatabase(dbFilepath);
            }
        }

        [TestMethod]
        public void TestQueryData()
        {
            string dbFilepath = "my.db";
            DeleteDatabase(dbFilepath);
            try
            {
                var manager = new SqliteDatabaseManager(dbFilepath);
                manager.Initialize();
                CreateTableTemplate createTableTemplate = new CreateTableTemplate();
                createTableTemplate.TableName = "student";
                createTableTemplate.Version = 1;
                createTableTemplate.CreateSql = string.Format("CREATE TABLE IF NOT EXISTS {0}(", "student") +
                    "name varchar(100) PRIMARY KEY," +
                    "age INTEGER)";

                PrivateObject obj = new PrivateObject(manager);
                obj.Invoke("CreateTableIfNotExists", createTableTemplate);
                var version = manager.GetTableVersion("student");
                Assert.AreEqual(1, version);

                List<SqlTemplate> sqlTemplates = new List<SqlTemplate>();
                for (int i = 0; i < 1000; i++)
                {
                    SqlTemplate sqlTemplate = new SqlTemplate();
                    string sql = string.Format("INSERT INTO student values(?, ?)");
                    sqlTemplate.SqlExpression = sql;
                    sqlTemplate.Params = new object[] { "student" + i, 20 };
                    sqlTemplates.Add(sqlTemplate);
                }

                int result = manager.ExecuteDML(sqlTemplates);
                Assert.AreEqual(1000, result);

                SqlTemplate querySqlTemplate = new SqlTemplate();
                querySqlTemplate.SqlExpression = "SELECT * FROM student LIMIT 200";

                var items = manager.QueryData(querySqlTemplate);
                Assert.AreEqual(200, items.Count());
            }
            finally
            {
                DeleteDatabase(dbFilepath);
            }
        }

        private void DeleteDatabase(string dbFilePath)
        {
            if (File.Exists(dbFilePath))
            {
                File.Delete(dbFilePath);
            }
        }

        private void DropTableVersion()
        {
            string sql = "DROP TABLE IF EXISTS table_version";
            SqlTemplate sqlTemplate = new SqlTemplate();
            sqlTemplate.SqlExpression = sql;
            this._manager.ExecuteNonQuery(sqlTemplate);
        }

        private bool IsTableExists(SqliteDatabaseManager manager, string tableName)
        {
            string sql = "SELECT count(0) FROM sqlite_master WHERE type='table' AND name='" + tableName + "'";
            SqlTemplate sqlTemplate = new SqlTemplate();
            sqlTemplate.SqlExpression = sql;
            var result = manager.ExecuteScalar(sqlTemplate);
            return "1".Equals(result.ToString());
        }
    }
}

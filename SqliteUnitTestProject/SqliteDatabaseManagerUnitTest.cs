using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqliteUtils.Models;
using SqliteUtils.Utils;
using System.Linq;

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
    }
}

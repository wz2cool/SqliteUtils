using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace SqliteUtils.Models
{
    public class SqliteSqlTemplate
    {
        public string SqlExpression { get; set; }
        public SQLiteParameter[] Params { get; set; }

        public SqliteSqlTemplate()
        {
            this.Params = new SQLiteParameter[0];
        }
    }
}

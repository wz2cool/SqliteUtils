﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace SqliteUtils.Models
{
    public class SqliteSqlTemplate
    {
        public string SqlExpression { get; set; }
        public IEnumerable<SQLiteParameter> Params { get; set; }
    }
}
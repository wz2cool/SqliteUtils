using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqliteUtils.Models
{
    public class TableInfo
    {
        [JsonProperty("tableName")]
        public string TableName { get; set; }
        [JsonProperty("columnNames")]
        public string[] ColumnNames { get; set; }
    }
}

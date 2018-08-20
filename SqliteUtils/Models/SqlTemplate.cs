using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqliteUtils.Models
{
    public class SqlTemplate
    {
        [JsonProperty("sqlExpression")]
        public string SqlExpression { get; set; }
        [JsonProperty("params")]
        public object[] Params { get; set; }
    }
}

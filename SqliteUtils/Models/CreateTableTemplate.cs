using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqliteUtils.Models
{
    public class CreateTableTemplate
    {
        [JsonProperty("tableName")]
        public string TableName { get; set; }
        [JsonProperty("version")]
        public int Version { get; set; }
        [JsonProperty("createSql")]
        public string CreateSql { get; set; }
    }
}

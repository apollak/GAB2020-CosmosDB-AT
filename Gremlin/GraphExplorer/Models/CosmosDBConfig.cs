using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GraphExplorer.Models
{
    public class CosmosDBConfig
    {
        public string CoreSQLEndpointUrl { get; set; }
        public string PrimaryKey { get; set; }
        public string Database { get; set; }
        public string PartitionPath { get; set; }
        public string GremlinEndpointUrl { get; set; }
        public int GremlinPort { get; set; }
    }
}

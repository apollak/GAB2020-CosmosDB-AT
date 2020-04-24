namespace GraphExplorer.Controllers
{
    using GraphExplorer.Models;
    using Gremlin.Net.Driver;
    using Gremlin.Net.Driver.Exceptions;
    using Gremlin.Net.Structure.IO.GraphSON;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [Route("api/[controller]")]
    public class GremlinController : Controller
    {
        private readonly CosmosDBConfig dbConfig;

        public GremlinController(IOptions<CosmosDBConfig> configSettings)
        {
            dbConfig = configSettings.Value;
        }

        public GremlinServer GetServer(string collectionId)
        {
            return new GremlinServer(dbConfig.GremlinEndpointUrl, dbConfig.GremlinPort, enableSsl: true,
                                        username: "/dbs/" + dbConfig.Database + "/colls/" + collectionId,
                                        password: dbConfig.PrimaryKey);
        }

        //https://localhost:44328/api/Gremlin?query=g.V()&collectionId=thehobbit
        [HttpGet]
        public async Task<dynamic> Get(string query, string collectionId)
        {
            List<dynamic> results = new List<dynamic>();
            GremlinServer server = GetServer(collectionId);
            using (GremlinClient gremlinClient = new GremlinClient(server, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType))
            {
                List<Task> tasks = new List<Task>();
                string[] queries = query.Split(';');
                //split query on ; to allow for multiple queries
                foreach (string q in queries)
                {
                    if (!string.IsNullOrEmpty(q))
                    {
                        string singleQuery = q.Trim();

                        await ExecuteQuery(gremlinClient, singleQuery)
                                .ContinueWith(
                                    (task) =>
                                    {
                                        results.Add(new { queryText = singleQuery, queryResult = task.Result });
                                    }
                                );
                    }
                }
            }
            return results;
        }

        private async Task<List<dynamic>> ExecuteQuery(GremlinClient client, string query)
        {
            List<dynamic> results = new List<dynamic>();

            ResultSet<dynamic> resultSet = await ExecuteRequest(client, query);
            if (resultSet != null && resultSet.Count != 0)
            {
                foreach (dynamic result in resultSet)
                {
                    results.Add(result);
                }

            }
            return results;
        }

        private async Task<ResultSet<dynamic>> ExecuteRequest(GremlinClient gremlinClient, string query)
        {
            try
            {
                return await gremlinClient.SubmitAsync<dynamic>(query);
            }
            catch (ResponseException)
            {
                return null;
            }
        }
    }
}
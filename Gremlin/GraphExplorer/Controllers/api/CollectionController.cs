namespace GraphExplorer.Controllers.Api
{
    using GraphExplorer.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Options;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [Route("api/[controller]")]
    public class CollectionController : Controller
    {
        private readonly CosmosDBConfig dbConfig;
        private readonly CosmosClient cosmosClient;

        public CollectionController(IOptions<CosmosDBConfig> configSettings)
        {
            dbConfig = configSettings.Value;
            cosmosClient = new CosmosClient(
                dbConfig.CoreSQLEndpointUrl,
                dbConfig.PrimaryKey);
        }

        //https://github.com/MaximBalaganskiy/AureliaDotnetTemplate
        //https://localhost:44328/api/Collection
        [HttpGet]
        public async Task<dynamic> GetCollections()
        {
            DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(dbConfig.Database, 400);
            Database database = databaseResponse.Database;
            FeedIterator<ContainerProperties> collectionIterator = database.GetContainerQueryIterator<ContainerProperties>();
            List<string> collections = new List<string>();
            while (collectionIterator.HasMoreResults)
            {
                FeedResponse<ContainerProperties> cosmosDBcollections = await collectionIterator.ReadNextAsync();
                foreach (var collection in cosmosDBcollections)
                {
                    collections.Add(collection.Id);
                }
            }
            return collections;
        }

        [HttpPost]
        public async Task CreateCollection([FromQuery]string name)
        {
            await CreateCollectionIfNotExistsAsync(name);
        }

        [HttpDelete]
        public async Task DeleteCollection(string name)
        {
            await DeleteCollectionAsync(name);
        }

        private async Task CreateCollectionIfNotExistsAsync(string collectionId)
        {
            DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(dbConfig.Database, 400);
            Database database = databaseResponse.Database;
            await database.CreateContainerIfNotExistsAsync(collectionId, dbConfig.PartitionPath);
        }

        private async Task DeleteCollectionAsync(string collectionId)
        {
            DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(dbConfig.Database, 400);
            Database database = databaseResponse.Database;
            Container collection = database.GetContainer(collectionId);
            await collection.DeleteContainerAsync();
        }
    }
}

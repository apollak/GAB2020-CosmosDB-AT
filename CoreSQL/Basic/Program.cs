using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Basic
{
    internal class Program
    {
        private readonly string EndpointUrl = Environment.GetEnvironmentVariable("EndpointUrl");
        private readonly string PrimaryKey = Environment.GetEnvironmentVariable("PrimaryKey");
        private CosmosClient cosmosClient;
        private Database database;
        private Container container;

        private readonly string databaseId = "FamilyDatabase";
        private readonly string containerId = "FamilyContainer";

        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Beginning operations...\n");
                Program p = new Program();
                await p.GetStartedDemoAsync();

            }
            catch (CosmosException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}", de.StatusCode, de);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        public async Task GetStartedDemoAsync()
        {
            // Create a new instance of the Cosmos Client
            // Explain Keys and Readonly Keys
            // Explain Resource Access
            // For Multimaster make sure to set the application region properly!
            cosmosClient = new CosmosClient(
             EndpointUrl,
             PrimaryKey,
             new CosmosClientOptions()
             {
                 ApplicationRegion = Regions.WestUS,
                 ApplicationPreferredRegions = new List<string> { 
                     Regions.WestUS,
                     Regions.WestUS2
                 }
             });
            await CreateDatabaseAsync();
            await CreateContainerAsync();
            await AddItemsToContainerAsync();
            await QueryItemsAsync();
            await QueryItemStreamsAsync();
            await QueryItemsLinqAsync();
        }

        private async Task CreateDatabaseAsync()
        {
            // If you obmit throughput here, throughput will be put on the container!
            database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId, 400);
            Console.WriteLine("Created Database: {0}\n", database.Id);
        }

        private async Task CreateContainerAsync()
        {
            // Specifiy "/LastName" as the partition key since we're storing family information, to ensure good distribution of requests and storage.
            // Unique Keys only unique within a partition!
            container = await database.CreateContainerIfNotExistsAsync(
                containerId, 
                "/LastName");

            //container = await database.CreateContainerIfNotExistsAsync(
            //    new ContainerProperties()
            //    {
            //         ConflictResolutionPolicy = new ConflictResolutionPolicy()
            //         {
            //             Mode= ConflictResolutionMode.LastWriterWins
            //             // ResolutionPath
            //             // ResolutionProcedure
            //         },
            //         PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V1,
            //         PartitionKeyPath = "/LastName"
            //    });

            Console.WriteLine("Created Container: {0}\n", container.Id);
        }

        private async Task AddItemsToContainerAsync()
        {
            // Create a family object for the Andersen family
            Family andersenFamily = new Family
            {
                Id = "Andersen.1",
                LastName = "Andersen",
                Parents = new Parent[]
                {
                   new Parent { FirstName = "Thomas" },
                   new Parent { FirstName = "Mary Kay" }
                },
                Children = new Child[]
                {
                    new Child
                    {
                        FirstName = "Henriette Thaulow",
                        Gender = "female",
                        Grade = 5,
                        Pets = new Pet[]
                        {
                            new Pet { GivenName = "Fluffy" }
                        }
                    }
                },
                Address = new Address { State = "WA", County = "King", City = "Seattle" },
                IsRegistered = false
            };

            try
            {
                // Create an item in the container representing the Andersen family. 
                // Note we provide the value of the partition key for this item, which is "Andersen".
                ItemResponse<Family> andersenFamilyResponse =
                    await container.CreateItemAsync<Family>(andersenFamily, new PartitionKey(andersenFamily.LastName));

                // Note that after creating the item, we can access the body of the item with the Resource property of the ItemResponse. 
                // We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n",
                    andersenFamilyResponse.Resource.Id,
                    andersenFamilyResponse.RequestCharge);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                Console.WriteLine("Item in database with id: {0} already exists\n", andersenFamily.Id);
            }
        }

        private async Task QueryItemsAsync()
        {
            string sqlQueryText = "SELECT * FROM c WHERE c.LastName = 'Andersen'";

            Console.WriteLine("Running query: {0}\n", sqlQueryText);

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

            FeedIterator<Family> queryResultSetIterator = container.GetItemQueryIterator<Family>(queryDefinition);

            List<Family> families = new List<Family>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<Family> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (Family family in currentResultSet)
                {
                    families.Add(family);
                    Console.WriteLine("\tRead {0}\n", family);
                }
            }
        }

        private async Task QueryItemStreamsAsync()
        {
            IQueryable<Family> query = 
                container.GetItemLinqQueryable<Family>(
                    requestOptions: new QueryRequestOptions()
                    {
                        PartitionKey = new PartitionKey("Andersen"),
                        MaxConcurrency = 1, /* For many items use -1 */
                        MaxItemCount = 1
                    }
                ).Where(f => f.Id == "Andersen.1");

            FeedIterator fIterator = query.ToStreamIterator();

            int totalCount = 0;
            Console.WriteLine("Running query: {0}\n", query.ToString());
            while (fIterator.HasMoreResults)
            {
                int count = 0;
                using ResponseMessage response = await fIterator.ReadNextAsync();
                Console.WriteLine($"Query Cost: {response.Headers.RequestCharge} RUs.");

                if (response.Diagnostics != null)
                {
                    Console.WriteLine($"ItemStreamFeed Diagnostics: {response.Diagnostics.ToString()}");
                }

                response.EnsureSuccessStatusCode();
                count++;
                using StreamReader sr = new StreamReader(response.Content);
                using JsonTextReader jtr = new JsonTextReader(sr);
                JsonSerializer jsonSerializer = new JsonSerializer();
                dynamic array = jsonSerializer.Deserialize<dynamic>(jtr);
                totalCount += array.Documents.Count;
            }
        }

        private async Task QueryItemsLinqAsync()
        {
            IQueryable<Family> query = container.GetItemLinqQueryable<Family>()
                            .Where(f => f.Id == "Andersen.1");

            FeedIterator<Family> fIterator = query.ToFeedIterator();

            Console.WriteLine("Running query: {0}\n", query.ToString());

            while (fIterator.HasMoreResults)
            {
                FeedResponse<Family> currentResultSet = await fIterator.ReadNextAsync();
                Console.WriteLine($"Query Cost: {currentResultSet.RequestCharge} RUs.");
                foreach (Family family in currentResultSet)
                {
                    Console.WriteLine("\tRead {0}\n", family);
                }
            }
        }

        private async Task DeleteDatabaseAndCleanupAsync()
        {
            DatabaseResponse databaseResourceResponse = await database.DeleteAsync();
            // Also valid: await this.cosmosClient.Databases["FamilyDatabase"].DeleteAsync();

            Console.WriteLine("Deleted Database: {0}\n", databaseId);

            //Dispose of CosmosClient
            cosmosClient.Dispose();
        }
    }
}

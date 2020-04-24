using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Spatial;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Spatial
{
    class Program
    {
        private readonly string EndpointUrl = Environment.GetEnvironmentVariable("EndpointUrl");
        private readonly string PrimaryKey = Environment.GetEnvironmentVariable("PrimaryKey");
        private CosmosClient cosmosClient;
        private Database database;
        private Container container;

        private readonly string databaseId = "FamilyDatabase";
        private readonly string containerId = "ShopContainer";

        private readonly string partitionKey = "/p";

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
            cosmosClient = new CosmosClient(EndpointUrl, PrimaryKey);
            await CreateDatabaseAsync();
            await CreateContainerAsync();
            await AddItemsToContainerAsync();
            QueryShops();
        }

        private async Task CreateDatabaseAsync()
        {
            var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId, 400);
            database = databaseResponse;
            Console.WriteLine("Created Database: {0}\n", database.Id);
        }

        private async Task CreateContainerAsync()
        {
            var containerResponse = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties()
                {
                    Id = containerId,
                    GeospatialConfig = new GeospatialConfig(GeospatialType.Geography),
                    PartitionKeyPath = partitionKey
                });

            container = await CreateIndexes(containerResponse);
            Console.WriteLine("Created Container: {0}\n", container.Id);
        }

        /// <summary>
        /// Create necessary indexes
        /// </summary>
        /// <param name="containerResponse"></param>
        /// <returns></returns>
        private async Task<Container> CreateIndexes(ContainerResponse containerResponse)
        {
            var indexingPolicy = containerResponse.Resource.IndexingPolicy;

            indexingPolicy.IndexingMode = IndexingMode.Consistent;

            // Add a spatial index
            if (indexingPolicy.SpatialIndexes.Count == 0)
            {
                SpatialPath spatialPath = new SpatialPath
                {
                    Path = "/location/*"
                };
                spatialPath.SpatialTypes.Add(SpatialType.Point);
                indexingPolicy.SpatialIndexes.Add(spatialPath);
            }

            // Add a composite index
            if (indexingPolicy.CompositeIndexes.Count == 0)
            {

                indexingPolicy.CompositeIndexes.Add(
                    new Collection<CompositePath> {
                        new CompositePath() { Path = "/name",
                            Order = CompositePathSortOrder.Ascending },
                        new CompositePath() { Path = "/founded",
                            Order = CompositePathSortOrder.Ascending } });
            }

            return await containerResponse.Container.ReplaceContainerAsync(containerResponse.Resource);
        }

        private TransactionalBatch InsertAllDocuments(TransactionalBatch batch)
        {
            TransactionalBatch result = batch;
            foreach (var shop in CreateShopList())
            {
                result.CreateItem<Shop>(shop);
                Console.WriteLine($"Added shop to batch request: {shop.Name}");
            }
            return result;
        }

        private async Task AddItemsToContainerAsync()
        {
            try
            {
                /* Classic Insertion
                foreach (var shop in CreateShopList())
                {
                    var resp = await container.CreateItemAsync<Shop>(shop);
                    Console.WriteLine($"Created {shop.Name} Client Time: {resp.Diagnostics.GetClientElapsedTime().TotalSeconds} \n");
                }
                */

                // ACID - Limitations (Transaction only scopes a single partition!)
                // - Payload cannot exceed 2MB as per the Azure Cosmos DB request size limit
                // - Maximum execution time is 5 seconds
                // - Limit of 100 operations per TransactionalBatch to make sure the performance is as expected and within SLAs.
                using (TransactionalBatchResponse batchResponse =
                        await InsertAllDocuments(
                            container.CreateTransactionalBatch(new PartitionKey("Austria")))
                        .ExecuteAsync())
                {
                    if (!batchResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Batch execution failed!\n");
                        // Handle and log exception
                    }
                    else
                    {
                        // Look up interested results - eg. via typed access on operation results
                        Console.WriteLine($"Transactionbatch completed with a charge of {batchResponse.RequestCharge} RUs.");
                        for (int i = 0; i < batchResponse.Count; i++)
                        {
                            TransactionalBatchOperationResult<Shop> opResult = batchResponse.GetOperationResultAtIndex<Shop>(i);
                            Console.WriteLine($"Shop {opResult.Resource.Name} created with {opResult.ETag} ETAG.");
                        }
                    }
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                Console.WriteLine("Items already existing\n");
            }
        }

        IReadOnlyList<Shop> CreateShopList()
        {
            List<Shop> shops = new List<Shop>()
            {
                new Shop(){Id="001", Name="Bäcker St. Stephan", FoundedAt=new DateTime(2010,06,23), Location= new Point(16.371943, 48.208831) },
                new Shop(){Id="002", Name="Beisl Innen", FoundedAt=new DateTime(2009,03,15), Location= new Point(16.368857, 48.209517) },
                new Shop(){Id="003", Name="Ringe und Schmuck", FoundedAt=new DateTime(2008,12,11), Location= new Point(16.360876, 48.209746) },
                new Shop(){Id="004", Name="Neue Buntstifte", FoundedAt=new DateTime(2007,02,05), Location= new Point(16.347207, 48.205556) },
                new Shop(){Id="005", Name="Taschen und Gürtel", FoundedAt=new DateTime(2006,03,10), Location= new Point(16.336715, 48.206214) },
                new Shop(){Id="006", Name="Baumschule", FoundedAt=new DateTime(2005,05,02), Location= new Point(16.297112, 48.199964) },
                new Shop(){Id="007", Name="Auhof Shopping", FoundedAt=new DateTime(2004,04,01), Location= new Point(16.227863, 48.206829) }
            };
            return shops;
        }

        private void QueryShops()
        {
            double range = 4;
            Point position = new Point(16.303797, 48.208631); // Flötzersteig

            Console.WriteLine($"Searching shops within the range of {range}km.");
            var query = container.GetItemLinqQueryable<Shop>(allowSynchronousQueryExecution: true)
                .Where(s => s.Location.Distance(position) < range * 1000);

            foreach (Shop shop in query)
            {
                Console.WriteLine($"Found shop {shop.Name}");
            }
        }
    }
}

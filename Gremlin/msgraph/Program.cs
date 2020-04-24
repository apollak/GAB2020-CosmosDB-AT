// Based on https://github.com/Azure-Samples/azure-cosmos-db-graph-gremlindotnet-getting-started
using Gremlin.Net.Driver;
using Gremlin.Net.Driver.Exceptions;
using Gremlin.Net.Structure.IO.GraphSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace msgraph
{
    internal class Program
    {
        // Gremlin Endpoint without prefix:
        //  Azure Portal - Gremlin Endpoint: wss://youraccount.gremlin.cosmos.azure.com:443/
        //  EndpointUrl = "youraccount.gremlin.cosmos.azure.com" 
        private static readonly string EndpointUrl = Environment.GetEnvironmentVariable("GraphEndpointUrl");
        private static readonly string PrimaryKey = Environment.GetEnvironmentVariable("GraphPrimaryKey");
        private static readonly int port = 443;
        private static readonly string database = "graphdb";
        private static readonly string container = "thehobbit";
        private static bool requestExit = false;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Gremlin Simple REPL Console V 0.1 \n");
            GremlinServer gremlinServer = GetServer();

            using (GremlinClient gremlinClient = new GremlinClient(gremlinServer, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType))
            {
                while (!requestExit)
                {
                    Console.Write(":> ");
                    string input = Console.ReadLine();
                    string[] commands = input.Split(';');
                    foreach (string command in commands)
                    {
                        string trimmedCommand = command.Trim();
                        if (!string.IsNullOrEmpty(trimmedCommand))
                        {
                            if (trimmedCommand.StartsWith(":"))
                            {
                                ProcessCommand(command);
                                if (requestExit)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                ResultSet<dynamic> resultSet = await ExecuteRequest(gremlinClient, command);
                                ProcessResultSet(resultSet);
                            }
                        }
                    }
                }
            }
        }

        private static void ProcessCommand(string command)
        {
            if (command.ToLower() == ":q" || command.ToLower() == ":quit")
            {
                requestExit = true;
            }

            if (command.ToLower() == ":cls")
            {
                Console.Clear();
            }
        }

        private static void ProcessResultSet(ResultSet<dynamic> resultSet)
        {
            if (resultSet == null)
            {
                return;
            }

            if (resultSet.Count > 0)
            {
                Console.WriteLine("\tResult:");
                foreach (dynamic result in resultSet)
                {
                    // The vertex results are formed as Dictionaries with a nested dictionary for their properties
                    string output = JsonConvert.SerializeObject(result);
                    Console.WriteLine($"\t{output}");
                }
                Console.WriteLine();
            }
            PrintStatusAttributes(resultSet.StatusAttributes);
            Console.WriteLine();
        }

        public static GremlinServer GetServer()
        {
            return new GremlinServer(EndpointUrl, port, enableSsl: true,
                                        username: "/dbs/" + database + "/colls/" + container,
                                        password: PrimaryKey);
        }

        private static async Task<ResultSet<dynamic>> ExecuteRequest(GremlinClient gremlinClient, string request)
        {
            try
            {
                return await gremlinClient.SubmitAsync<dynamic>(request);
            }
            catch (ResponseException e)
            {
                Console.WriteLine("\tRequest Error!");
                Console.WriteLine($"\tStatusCode: {e.StatusCode}");

                // On error, ResponseException.StatusAttributes will include the common StatusAttributes for successful requests, as well as
                // additional attributes for retry handling and diagnostics.
                // These include:
                //  x-ms-retry-after-ms         : The number of milliseconds to wait to retry the operation after an initial operation was throttled. This will be populated when
                //                              : attribute 'x-ms-status-code' returns 429.
                //  x-ms-activity-id            : Represents a unique identifier for the operation. Commonly used for troubleshooting purposes.
                PrintStatusAttributes(e.StatusAttributes);
                Console.WriteLine($"\t[\"x-ms-retry-after-ms\"] : { GetValueAsString(e.StatusAttributes, "x-ms-retry-after-ms")}");
                Console.WriteLine($"\t[\"x-ms-activity-id\"] : { GetValueAsString(e.StatusAttributes, "x-ms-activity-id")}");
                return null;
            }
        }

        private static void PrintStatusAttributes(IReadOnlyDictionary<string, object> attributes)
        {
            Console.WriteLine($"\tStatusAttributes:");
            Console.WriteLine($"\t[\"x-ms-status-code\"] : { GetValueAsString(attributes, "x-ms-status-code")}");
            Console.WriteLine($"\t[\"x-ms-total-request-charge\"] : { GetValueAsString(attributes, "x-ms-total-request-charge")}");
        }

        public static string GetValueAsString(IReadOnlyDictionary<string, object> dictionary, string key)
        {
            return JsonConvert.SerializeObject(GetValueOrDefault(dictionary, key));
        }

        public static object GetValueOrDefault(IReadOnlyDictionary<string, object> dictionary, string key)
        {
            if (dictionary.ContainsKey(key))
            {
                return dictionary[key];
            }

            return null;
        }

    }
}

using Microsoft.Azure.Cosmos.Spatial;
using Newtonsoft.Json;
using System;

namespace Spatial
{
    public class Shop
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("location")]
        public Point Location { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("founded")]
        public DateTime FoundedAt { get; set; }

        [JsonProperty("p")]
        public string PartitionKey { get; set; } = "Austria";
    }
}

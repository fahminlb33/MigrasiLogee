using Newtonsoft.Json;

namespace MigrasiLogee.Models
{
    public class MongoClusterInfo
    {
        [JsonProperty("setName")]
        public string SetName { get; set; }

        [JsonProperty("ismaster")]
        public bool IsMaster { get; set; }

        [JsonProperty("primary")]
        public string Primary { get; set; }

        [JsonProperty("me")]
        public string Me { get; set; }

        [JsonProperty("hosts")]
        public string[] Hosts { get; set; }
    }
}

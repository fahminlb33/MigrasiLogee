using Newtonsoft.Json;

namespace MigrasiLogee.Services
{
    public class MongoConnectionStatistics
    {
        [JsonProperty("current")]
        public long Current { get; set; }

        [JsonProperty("available")]
        public long Available { get; set; }

        [JsonProperty("totalCreated")]
        public long TotalCreated { get; set; }

        [JsonProperty("active")]
        public long Active { get; set; }

        [JsonProperty("exhaustIsMaster")]
        public long ExhaustIsMaster { get; set; }

        [JsonProperty("exhaustHello")]
        public long ExhaustHello { get; set; }

        [JsonProperty("awaitingTopologyChanges")]
        public long AwaitingTopologyChanges { get; set; }
    }
}

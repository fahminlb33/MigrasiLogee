using System.Collections.Generic;
using Newtonsoft.Json;

namespace MigrasiLogee.Models
{
    public class KafkaReassignmentManifest
    {
        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("partitions")]
        public List<KafkaPartition> Partitions { get; set; }
    }
}

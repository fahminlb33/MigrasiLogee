using Newtonsoft.Json;

namespace MigrasiLogee.Models
{
    public class KafkaPartition
    {
        [JsonProperty("topic")]
        public string Topic { get; set; }

        [JsonProperty("partition")]
        public int Partition { get; set; }

        [JsonProperty("replicas")]
        public int[] Replicas { get; set; }
    }
}

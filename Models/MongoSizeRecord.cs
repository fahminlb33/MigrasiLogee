namespace MigrasiLogee.Models
{
    public class MongoSizeRecord
    {
        public string PodName { get; set; }
        public string Database { get; set; }
        public string Collection { get; set; }
        public float DocumentSize { get; set; }
        public float AverageDocumentSize { get; set; }
        public float CollectionSize { get; set; }
    }
}

namespace MigrasiLogee.Models
{
    public class MongoSizeRecord
    {
        public string PodName { get; set; }
        public string Database { get; set; }
        public string Collection { get; set; }
        public int DocumentSize { get; set; }
        public int AverageDocumentSize { get; set; }
        public int CollectionSize { get; set; }
    }
}

using System.Collections.Generic;
using System.Linq;
using MigrasiLogee.Exceptions;
using MigrasiLogee.Infrastructure;
using Newtonsoft.Json;

namespace MigrasiLogee.Services
{
    public class MongoShell
    {
        public static readonly string[] InternalDatabase = {"admin", "local", "config"};

        public string MongoExecutable { get; set; }
        public string Host { get; set; }
        public string AdminPassword { get; set; }

        public static bool IsInternalDatabase(string database)
        {
            return InternalDatabase.Contains(database);
        }

        public IEnumerable<string> GetDatabaseNames()
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
                Arguments = BuildMongoCommand(null, "db.getMongo().getDBNames()")
            };

            var (standardOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(standardOutput);
            
            return JsonConvert.DeserializeObject<List<string>>(standardOutput);
        }

        public IEnumerable<string> GetCollectionNames(string databaseName)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
                Arguments = BuildMongoCommand(databaseName, "db.getCollectionNames()")
            };

            var (standardOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(standardOutput);
            
            return JsonConvert.DeserializeObject<List<string>>(standardOutput);
        }

        public (int documentSize, int avgDocumentSize, int collectionSize) GetCollectionSize(string databaseName, string collectionName)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
            };

            // count document size
            var eval = $"Object.bsonsize(db.getCollection('{collectionName}').findOne())";
            process.Arguments = BuildMongoCommand(databaseName, eval);
            var (documentSizeOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(documentSizeOutput);

            // count document
            eval = $"db.getCollection('{collectionName}').count()";
            process.Arguments = BuildMongoCommand(databaseName, eval);
            var (documentCountOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(documentCountOutput);

            // count collection size
            eval = $"db.getCollection('{collectionName}').dataSize()";
            process.Arguments = BuildMongoCommand(databaseName, eval);
            var (collectionSizeOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(collectionSizeOutput);

            var documentSize = ParseInt(documentSizeOutput);
            var collectionSize = ParseInt(collectionSizeOutput);
            var avgDocumentSize = (int)((double)collectionSize / ParseInt(documentCountOutput));
            
            return (documentSize, avgDocumentSize, collectionSize);
        }

        private string BuildMongoCommand(string databaseName, string eval)
        {
            databaseName = string.IsNullOrWhiteSpace(databaseName) ? "" : databaseName + " ";
            return $"{databaseName}--host {Host} -u admin -p {AdminPassword} --authenticationDatabase=admin --eval \"{eval}\" --quiet";
        }

        private int ParseInt(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? 0 : int.Parse(s);
        }

        private void ValidateOutput(string output)
        {
            if (output.Contains("SocketException"))
            {
                throw new MongoConnectionError(output);
            }

            if (output.Contains("Error"))
            {
                throw new MongoException(output);
            }
        }
    }
}

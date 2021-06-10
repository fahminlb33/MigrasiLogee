using System.Collections.Generic;
using System.Linq;
using MigrasiLogee.Exceptions;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using Newtonsoft.Json;

namespace MigrasiLogee.Services
{
    public record MongoSecret(string Username, string Password, string AdminPassword);

    public record MongoCollectionStatistics(int DocumentSize, int AverageDocumentSize, int CollectionSize);

    public class MongoClient
    {
        public const string AdminUser = "admin";
        public const string AdminDatabase = "admin";
        public static readonly string[] InternalDatabase = { "admin", "local", "config" };

        public string MongoExecutable { get; set; }
        public string MongoDumpExecutable { get; set; }

        public static bool IsInternalDatabase(string database)
        {
            return InternalDatabase.Contains(database);
        }

        public bool IsMongoUp(string host, MongoSecret secret)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
                Arguments = BuildMongoCommand(host, 
                    null, 
                    AdminUser, 
                    secret.AdminPassword, 
                    AdminDatabase, 
                    "db.runCommand('ping')")
            };

            var (_, _, exitCode) = process.StartWaitWithRedirect();
            return exitCode == 0;
        }

        public IEnumerable<string> GetDatabaseNames(string host, MongoSecret secret)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
                Arguments = BuildMongoCommand(host, 
                    null, 
                    AdminUser, 
                    secret.AdminPassword, 
                    AdminDatabase, 
                    "db.getMongo().getDBNames()")
            };

            var (standardOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(standardOutput);

            return JsonConvert.DeserializeObject<List<string>>(standardOutput);
        }

        public IEnumerable<string> GetCollectionNames(string host, string databaseName, MongoSecret secret)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
                Arguments = BuildMongoCommand(host, databaseName, AdminUser, secret.AdminPassword, AdminDatabase, "db.getCollectionNames()")
            };

            var (standardOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(standardOutput);

            return JsonConvert.DeserializeObject<List<string>>(standardOutput);
        }

        public MongoCollectionStatistics GetCollectionSize(string host, MongoSecret secret, string databaseName, string collectionName)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
            };

            // count document size
            var eval = $"Object.bsonsize(db.getCollection('{collectionName}').findOne())";
            process.Arguments = BuildMongoCommand(host, databaseName, AdminUser, secret.AdminPassword, AdminDatabase, eval);
            var (documentSizeOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(documentSizeOutput);

            // count document
            eval = $"db.getCollection('{collectionName}').count()";
            process.Arguments = BuildMongoCommand(host, databaseName, AdminUser, secret.AdminPassword, AdminDatabase, eval);
            var (documentCountOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(documentCountOutput);

            // count collection size
            eval = $"db.getCollection('{collectionName}').dataSize()";
            process.Arguments = BuildMongoCommand(host, databaseName, AdminUser, secret.AdminPassword, AdminDatabase, eval);
            var (collectionSizeOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(collectionSizeOutput);

            var documentSize = StringHelpers.ParseInt(documentSizeOutput);
            var collectionSize = StringHelpers.ParseInt(collectionSizeOutput);
            var avgDocumentSize = collectionSize / StringHelpers.ParseInt(documentCountOutput);

            return new(documentSize, avgDocumentSize, collectionSize);
        }

        public int GetActiveConnections(string host, MongoSecret secret, string databaseName)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
                Arguments = BuildMongoCommand(host, databaseName, AdminUser, secret.AdminPassword, AdminDatabase, "db.serverStatus().connections.active")
            };

            var (standardOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(standardOutput);

            return int.Parse(standardOutput);
        }

        public static MongoSecret ParseSecret(IDictionary<string, string> dict)
        {
            return new(dict["MONGODB_USER"], dict["MONGODB_PASSWORD"], dict["MONGODB_ADMIN_PASSWORD"]);
        }

        private static string BuildMongoCommand(string host, string databaseName, string username, string password, string authenticationDatabase, string eval)
        {
            databaseName = string.IsNullOrWhiteSpace(databaseName) ? "" : databaseName + " ";
            return $"{databaseName}--host {host} -u {username} -p {password} --authenticationDatabase {authenticationDatabase} --eval \"{eval}\" --quiet";
        }

        private static void ValidateOutput(string output)
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

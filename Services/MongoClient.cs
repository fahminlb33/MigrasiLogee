using System;
using System.Collections.Generic;
using System.Linq;
using MigrasiLogee.Exceptions;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using MigrasiLogee.Models;
using Newtonsoft.Json;
using Spectre.Console;

namespace MigrasiLogee.Services
{
    public record MongoSecret(string Username, string Password, string AdminPassword);

    public record MongoCollectionStatistics(int DocumentSize, int AverageDocumentSize, int CollectionSize);

    public class MongoClient
    {
        public const string MongoExecutableName = "mongo";
        public const string MongoDumpExecutableName = "mongodump";

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

        public MongoClusterInfo GetClusterInfo(string host)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
                Arguments = $"--host {host} --quiet --eval \"JSON.stringify(db.runCommand('ismaster'))\""
            };

            var (standardOutput, _, _) = process.StartWaitWithRedirect();
            try
            {
                return JsonConvert.DeserializeObject<MongoClusterInfo>(standardOutput);
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
                return null;
            }
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
            var avgDocumentSize = collectionSize == 0 ? 0 : collectionSize / StringHelpers.ParseInt(documentCountOutput);

            return new(documentSize, avgDocumentSize, collectionSize);
        }

        public MongoConnectionStatistics GetConnections(string host, MongoSecret secret, string databaseName)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoExecutable,
                Arguments = BuildMongoCommand(host, databaseName, AdminUser, secret.AdminPassword, AdminDatabase, "db.serverStatus().connections")
            };

            var (standardOutput, _, _) = process.StartWaitWithRedirect();
            ValidateOutput(standardOutput);

            return JsonConvert.DeserializeObject<MongoConnectionStatistics>(standardOutput);
        }

        public void DumpDatabase(string host, MongoSecret secret, string databaseName, string outputPath)
        {
            using var process = new ProcessJob
            {
                ExecutableName = MongoDumpExecutableName,
                Arguments = BuildMongoDumpCommand(host, databaseName, AdminUser, secret.AdminPassword, AdminDatabase, outputPath)
            };

            var (standardOutput, standardError, _) = process.StartWaitWithRedirect();
            ValidateOutput(standardOutput);
            ValidateOutput(standardError);
        }

        public static MongoSecret ParseSecret(IDictionary<string, string> dict)
        {
            return new(dict["MONGODB_USER"], dict["MONGODB_PASSWORD"], dict["MONGODB_ADMIN_PASSWORD"]);
        }

        #region Private Methods

        private static string BuildMongoAuthCommand(string host, string username, string password, string authenticationDatabase)
        {
            return $"--host {host} -u {username} -p {password} --authenticationDatabase {authenticationDatabase}";
        }

        private static string BuildMongoCommand(string host, string databaseName, string username, string password, string authenticationDatabase, string eval)
        {
            databaseName = string.IsNullOrWhiteSpace(databaseName) ? "" : databaseName + " ";
            var auth = BuildMongoAuthCommand(host, username, password, authenticationDatabase);
            return $"{databaseName}{auth} --eval \"{eval}\" --quiet";
        }

        private static string BuildMongoDumpCommand(string host, string databaseName, string username, string password, string authenticationDatabase, string outputPath)
        {
            var auth = BuildMongoAuthCommand(host, username, password, authenticationDatabase);
            return $"{auth} --db {databaseName} --out=\"{outputPath}\"";
        }

        private static void ValidateOutput(string output)
        {
            if (output.ToLowerInvariant().Contains("socketexception"))
            {
                throw new MongoConnectionError(output);
            }

            if (output.ToLowerInvariant().Contains("error"))
            {
                throw new MongoException(output);
            }
        } 

        #endregion
    }
}

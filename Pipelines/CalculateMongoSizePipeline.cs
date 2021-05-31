using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using MigrasiLogee.Models;
using MigrasiLogee.Services;

namespace MigrasiLogee.Pipelines
{
    public record CalculateMongoSizeParameters(string ProjectName, string MongoPrefix, string Output);
    public class CalculateMongoSizePipeline
    {
        //var processor = new StagingUpChecker();
        //await processor.CheckAll();
        //var rootComand = new RootCommand("Count MongoDB database size per collection.")
        //{
        //    new Option<string>(new[] {"--project-name", "-p"}, () => string.Empty,
        //        "Project name in OpenShift"),
        //    new Option<string>(new[] {"--mongo-prefix", "-m"}, () => "mongo",
        //        "Mongo service prefix (if no prefix, then specify full service name)."),
        //    new Option<string>(new[] {"--output", "-o"}, () => "output.csv", 
        //        "Output filename. Default: output.csv")
        //};

        //rootComand.TreatUnmatchedTokensAsErrors = true;

        //rootComand.Handler = CommandHandler.Create<string, string, string>(BootstrapMain);
        //return rootComand.Invoke(args);
        public Task<bool> VerifyPrerequisite()
        {
            throw new NotImplementedException();
        }

        public Task Run(CalculateMongoSizeParameters parameters)
        {
            var oc = new OpenShiftClient
            {
                OcExecutable = "oc.exe",
                ProjectName = parameters.ProjectName
            };

            Console.WriteLine("Discovering pods and secrets...");
            var pods = oc.GetPodNames().Where(x => x.Contains(parameters.MongoPrefix));
            var secrets = oc.GetSecretNames().ToList();
            var records = new List<MongoSizeRecord>();

            foreach (var pod in pods)
            {
                Console.WriteLine();
                Console.WriteLine($" ----- Selected pod: {pod} -----");

                var serviceName = OpenShiftClient.PodToServiceName(pod);
                var secret = secrets.First(x => x.Contains(serviceName));

                Console.WriteLine($"Secret found for service {pod} as {secret}");
                var secretDict = oc.GetSecret(secret);

                Console.WriteLine("Starting port-forward on port 27017...");
                using var job = oc.PortForward(pod, 27099, 27017);

                var portForwardStatus = job.EnsureStarted();
                Console.WriteLine("Port-forward status: " + portForwardStatus);

                if (!portForwardStatus)
                {
                    Console.WriteLine($"Skipping pod {pod} because port-forward can't be made.");
                    continue;
                }

                var mongo = new MongoShell
                {
                    Host = "localhost:27099",
                    AdminPassword = secretDict["MONGODB_ADMIN_PASSWORD"],
                    MongoExecutable = "mongo.exe"
                };

                Console.WriteLine("Discovering databases...");
                var databases = mongo.GetDatabaseNames().ToList();
                foreach (var database in databases.Where(database => !MongoShell.IsInternalDatabase(database)))
                {
                    Console.WriteLine();
                    Console.WriteLine("Processing database: " + database);

                    var collections = mongo.GetCollectionNames(database);
                    foreach (var collection in collections)
                    {
                        if (collection.Contains("system"))
                        {
                            continue;
                        }

                        Console.WriteLine("Processing collection: " + collection);
                        var (documentSize, avgDocumentSize, collectionSize) = mongo.GetCollectionSize(database, collection);

                        records.Add(new MongoSizeRecord
                        {
                            PodName = serviceName,
                            Database = database,
                            Collection = collection,
                            AverageDocumentSize = avgDocumentSize,
                            DocumentSize = documentSize,
                            CollectionSize = collectionSize
                        });
                    }
                }

                job.StopJob();
            }

            Console.WriteLine();
            Console.WriteLine(" ----- Writing data ------");

            using var streamWriter = new StreamWriter(parameters.Output);
            using var csvWriter = new CsvWriter(streamWriter, CultureInfo.GetCultureInfo("en-US"));
            csvWriter.WriteRecords(records);

            Console.WriteLine(" ----- OK ------");
            return Task.CompletedTask;
        }
    }
}

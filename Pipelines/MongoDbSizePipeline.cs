using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using CsvHelper;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using MigrasiLogee.Models;
using MigrasiLogee.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MigrasiLogee.Pipelines
{
    public record CalculateMongoSizeParameters(string ProjectName, string MongoPrefix, string Output);

    public class CalculateMongoDbSizeSettings : CommandSettings
    {
        [CommandArgument(0, "<PROJECT_NAME>")]
        [Description("Project name containing deployments to scale")]
        public string ProjectName { get; set; }

        [CommandArgument(1, "<OUTPUT_PATH>")]
        [Description("Full path to output file (CSV)")]
        public string OutputPath { get; set; }

        [CommandOption("-p|--prefix <PREFIX>")]
        [Description("Only process deployment with this prefix. If no prefix is specified, then it will try all deployment in the project")]
        public string Prefix { get; set; }

        [CommandOption("--oc <OC_PATH>")]
        [Description("Relative/full path to '" + OpenShiftClient.OcExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string OcPath { get; set; }

        [CommandOption("--mongo <MONGO_PATH>")]
        [Description("Relative/full path to '" + MongoClient.MongoDumpExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string MongoPath { get; set; }
    }

    public class MongoDbSizePipeline : PipelineBase<CalculateMongoDbSizeSettings>
    {
        private readonly OpenShiftClient _oc = new();
        private readonly MongoClient _mongo = new();

        private const int LocalPort = 27099;
        private const int RemotePort = 27017;
        private static readonly string ForwardedHost = $"localhost:{LocalPort}";

        protected override bool ValidateState(CommandContext context, CalculateMongoDbSizeSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ProjectName))
            {
                MessageWriter.ArgumentNotSpecifiedMessage("<PROJECT_NAME>");
                return false;
            }

            _oc.ProjectName = settings.ProjectName;

            var ocPath = DependencyLocator.WhereExecutable(settings.OcPath, OpenShiftClient.OcExecutableName);
            if (ocPath == null)
            {
                MessageWriter.ExecutableNotFoundMessage(OpenShiftClient.OcExecutableName, "--oc");
                return false;
            }

            _oc.OcExecutable = ocPath;

            var mongoPath = DependencyLocator.WhereExecutable(settings.MongoPath, MongoClient.MongoExecutableName);
            if (mongoPath == null)
            {
                MessageWriter.ExecutableNotFoundMessage(MongoClient.MongoExecutableName, "--mongo");
                return false;
            }

            _mongo.MongoExecutable = mongoPath;

            return true;
        }

        protected override void PreRun(CommandContext context, CalculateMongoDbSizeSettings settings)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Render(new Text("{ MongoDB Collection Size Measurement }").Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            AnsiConsole.WriteLine("Project : {0}", settings.ProjectName);
            AnsiConsole.WriteLine("Save to : {0}", settings.OutputPath);
            AnsiConsole.WriteLine();
        }

        protected override int Run(CommandContext context, CalculateMongoDbSizeSettings settings)
        {
            Console.WriteLine("Discovering pods and secrets...");
            var pods = _oc.GetPodNames().Where(x => x.Contains(settings.Prefix)).ToList();
            var secrets = _oc.GetSecretNames().ToList();
            var records = new List<MongoSizeRecord>();

            foreach (var pod in pods)
            {
                Console.WriteLine();
                Console.WriteLine($" ----- Selected pod: {pod} -----");

                var serviceName = OpenShiftClient.PodToServiceName(pod);
                var secret = secrets.First(x => x.Contains(serviceName));

                Console.WriteLine($"Secret found for service {pod} as {secret}");
                var mongoSecret = MongoClient.ParseSecret(_oc.GetSecret(secret));

                Console.WriteLine($"Starting port-forward on port {LocalPort} to {RemotePort}...");
                using var job = _oc.PortForward(pod, LocalPort, RemotePort);

                bool isMongoUp;
                var checkCount = 0;
                do
                {
                    isMongoUp = _mongo.IsMongoUp(ForwardedHost, mongoSecret);

                    if (!isMongoUp) Thread.Sleep(1000);
                    checkCount++;
                } while (!isMongoUp && checkCount < 3);

                if (!isMongoUp)
                {
                    Console.WriteLine($"Skipping pod {pod} because port-forward can't be made or mongo cannot connect to pod.");
                    job.StopJob();
                    continue;
                }

                Console.WriteLine("Discovering databases...");
                var databases = _mongo.GetDatabaseNames(ForwardedHost, mongoSecret).ToList();
                foreach (var database in databases.Where(database => !MongoClient.IsInternalDatabase(database)))
                {
                    Console.WriteLine();
                    Console.WriteLine("Processing database: " + database);

                    var collections = _mongo.GetCollectionNames(ForwardedHost, database, mongoSecret).ToList();
                    foreach (var collection in collections)
                    {
                        if (collection.Contains("system"))
                        {
                            continue;
                        }

                        Console.WriteLine("Processing collection: " + collection);
                        var stats = _mongo.GetCollectionSize(ForwardedHost, mongoSecret, database, collection);

                        records.Add(new MongoSizeRecord
                        {
                            PodName = serviceName,
                            Database = database,
                            Collection = collection,
                            DocumentSize = stats.DocumentSize,
                            AverageDocumentSize = stats.AverageDocumentSize,
                            CollectionSize = stats.CollectionSize
                        });
                    }
                }

                job.StopJob();
            }

            Console.WriteLine();
            Console.WriteLine(" ----- Writing data ------");

            using var streamWriter = new StreamWriter(settings.OutputPath);
            using var csvWriter = new CsvWriter(streamWriter, CultureInfo.GetCultureInfo("en-US"));
            csvWriter.WriteRecords(records);

            Console.WriteLine(" ----- OK ------");
            return 0;
        }
    }
}

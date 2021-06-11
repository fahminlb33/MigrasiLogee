using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using MigrasiLogee.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MigrasiLogee.Pipelines
{
    public class DumpMongoDbSettings : CommandSettings
    {
        [CommandArgument(0, "<PROJECT_NAME>")]
        [Description("Project name containing deployments to scale")]
        public string ProjectName { get; set; }

        [CommandArgument(1, "<OUTPUT_PATH>")]
        [Description("Full path to output directory")]
        public string OutputPath { get; set; }

        [CommandOption("-p|--prefix <PREFIX>")]
        [Description("Only process deployment with this prefix. If no prefix is specified, then it will try all deployment in the project")]
        public string Prefix { get; set; }

        [CommandOption("--oc <OC_PATH>")]
        [Description("Relative/full path to '" + OpenShiftClient.OcExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string OcPath { get; set; }

        [CommandOption("--mongo <MONGO_PATH>")]
        [Description("Relative/full path to '" + MongoClient.MongoExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string MongoPath { get; set; }

        [CommandOption("--mongodump <MONGODUMP_PATH>")]
        [Description("Relative/full path to '" + MongoClient.MongoDumpExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string MongoDumpPath { get; set; }
    }

    public class DumpMongoDbPipeline : PipelineBase<DumpMongoDbSettings>
    {
        private readonly OpenShiftClient _oc = new();
        private readonly MongoClient _mongo = new();

        protected override bool ValidateState(CommandContext context, DumpMongoDbSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ProjectName))
            {
                MessageWriter.ArgumentNotSpecifiedMessage("<PROJECT_NAME>");
                return false;
            }

            _oc.ProjectName = settings.ProjectName;

            if (string.IsNullOrWhiteSpace(settings.OutputPath))
            {
                MessageWriter.ArgumentNotSpecifiedMessage("<OUTPUT_PATH>");
                return false;
            }

            settings.OutputPath = Path.GetFullPath(settings.OutputPath);

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

            var mongodumpPath = DependencyLocator.WhereExecutable(settings.MongoDumpPath, MongoClient.MongoDumpExecutableName);
            if (mongodumpPath == null)
            {
                MessageWriter.ExecutableNotFoundMessage(MongoClient.MongoDumpExecutableName, "--mongodump");
                return false;
            }

            _mongo.MongoDumpExecutable = mongodumpPath;

            return true;
        }

        protected override void PreRun(CommandContext context, DumpMongoDbSettings settings)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Render(new Text("{ MongoDB ULTRA-INSTINCT Dumper }").Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            AnsiConsole.WriteLine("Project : {0}", settings.ProjectName);
            AnsiConsole.WriteLine();
        }

        protected override int Run(CommandContext context, DumpMongoDbSettings settings)
        {
            AnsiConsole.WriteLine("Creating directory if not exists...");
            Directory.CreateDirectory(settings.OutputPath);

            AnsiConsole.WriteLine("Discovering pods...");
            var pods = _oc.GetPodNames().Where(x => x.Contains(settings.Prefix)).ToList();
            AnsiConsole.MarkupLine($"Found [yellow]{pods.Count}[/] pods.");
            
            AnsiConsole.WriteLine("Discovering secrets...");
            var secrets = _oc.GetSecretNames().ToList();
            var table = new Table().LeftAligned();

            AnsiConsole.Live(table)
                .Overflow(VerticalOverflow.Ellipsis)
                .Start(ctx =>
                    {
                        table.AddColumn("Pod");
                        table.AddColumn("Database");
                        table.AddColumn("Status");
                        ctx.Refresh();

                        foreach (var pod in pods)
                        {
                            var serviceName = OpenShiftClient.PodToServiceName(pod);
                            var secret = secrets.First(x => x.Contains(serviceName));
                            var mongoSecret = MongoClient.ParseSecret(_oc.GetSecret(secret));
                            using var job = _oc.PortForward(pod, NetworkHelpers.LocalMongoPort, NetworkHelpers.RemoteMongoPort);

                            bool isMongoUp;
                            var checkCount = 0;
                            do
                            {
                                isMongoUp = _mongo.IsMongoUp(NetworkHelpers.ForwardedMongoHost, mongoSecret);

                                if (!isMongoUp) Thread.Sleep(1000);
                                checkCount++;
                            } while (!isMongoUp && checkCount < 3);

                            if (!isMongoUp)
                            {
                                table.AddRow(pod, "Can't port-forward or access database.", "", "[yellow]Idk[/]");
                                job.StopJob();
                                continue;
                            }

                            var databases = _mongo.GetDatabaseNames(NetworkHelpers.ForwardedMongoHost, mongoSecret).ToList();
                            foreach (var database in databases.Where(database => !MongoClient.IsInternalDatabase(database)))
                            {
                                var statusMarkup = "";
                                try
                                {
                                    _mongo.DumpDatabase(NetworkHelpers.ForwardedMongoHost, mongoSecret, database, settings.OutputPath);
                                    statusMarkup = "[green]OK[/]";
                                }
                                catch (Exception e)
                                {
                                    statusMarkup = $"[red]Error: {e.Message.TrimLength(20)}[/]";
                                }

                                table.AddRow(pod, database, statusMarkup);
                                ctx.Refresh();
                            }

                            job.StopJob();
                        }
                    }
                );

            return 0;
        }
    }
}

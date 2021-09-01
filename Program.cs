using MigrasiLogee.Pipelines;
using Spectre.Console;
using Spectre.Console.Cli;
#if DEBUG
using System.Diagnostics;
#endif

namespace MigrasiLogee
{
    class Program
    {
        static int Main(string[] args)
        {
#if DEBUG
            Debugger.Launch();
#endif

            var app = new CommandApp();
            app.Configure(config =>
            {
                config.SetApplicationName("migrain");
                config.AddCommand<ScalePodsPipeline>("scale")
                    .WithDescription("Scales Kubernetes deployment replicas")
                    .WithExample(new[] { "scale", "logeect-stage", "--mode", "oc", "--replicas", "0" })
                    .WithExample(new[] { "scale", "logeect-stage", "--mode", "k3s", "--replicas", "0", "--kubeconfig", "logeect.yml" });

                config.AddCommand<DnsPropagationPipeline>("dns-propagation")
                    .WithDescription("Check DNS propagation after Ingress configuration")
                    .WithExample(new[] { "dns-propagation", "domains.csv", "-c", "vsan-cname.playcourt.id" })
                    .WithExample(new[] { "dns-propagation", "domains.csv", "-a", "69.69.69.69" });

                config.AddCommand<ServiceUptimePipeline>("uptime")
                    .WithDescription("Check if a service is accessible from internet using public URI")
                    .WithExample(new[] { "uptime", "ingress-urls.csv", "-m", "static", "-i", "69.69.69.69" })
                    .WithExample(new[] { "uptime", "ingress-urls.csv", "-m", "dynamic", "-d", "8.8.8.8" });

                config.AddCommand<ChangeMongoPrimaryForwarderPipeline>("mongo-primary")
                    .WithDescription("Check if the MongoDB forwarder is connected to master, if not then update the forwarder to connect to master")
                    .WithExample(new[] { "mongo-primary", "logee-prod", "mongo-forwarder" });

                config.AddCommand<MongoDbSizePipeline>("mongo-size")
                    .WithDescription("Calculate MongoDB database size")
                    .WithExample(new[] { "mongo-size", "logee-prod", "mongos.csv", "-p", "mongo" });

                config.AddCommand<MongoDbActiveConnectionPipeline>("mongo-connection")
                    .WithDescription("Get MongoDB active connection")
                    .WithExample(new[] { "mongo-connection", "logee-prod", "-p", "mongo" });

                config.AddCommand<MongoDbDumpPipeline>("mongo-dump")
                    .WithDescription("Dump all MongoDB database")
                    .WithExample(new[] { "mongo-dump", "logee-prod", @"D:\backup", "-p", "mongo" });

                config.AddCommand<AlterKafkaPartitionPipeline>("kafka-replicas")
                    .WithDescription("Alter Kafka replication factor and partitions to 2 partition and 2 replication factor in 3 nodes")
                    .WithExample(new[] { "kafka-replicas", "logee-stage", "-d", "deployment/kafka-broker1-forwarder", "-p", "9094", "-c", "kafka.conf" });
            });

            AnsiConsole.Render(new FigletText("MigrasiLogee").LeftAligned().Color(Color.DarkOrange3_1));
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("MigrasiLogee - [red]MIGRA[/]si is pa[red]IN[/].");
            AnsiConsole.WriteLine();

            return app.Run(args);
        }
    }
}

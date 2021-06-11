#if DEBUG
using System.Diagnostics;
#endif
using MigrasiLogee.Pipelines;
using Spectre.Console;
using Spectre.Console.Cli;

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
                    .WithDescription("Scales Kubernetes deployment replicas");

                config.AddCommand<VerifyServiceUptimePipeline>("uptime")
                    .WithDescription("Check if a service is accessible from internet using public URI")
                    .WithExample(new []{"ingress-urls.csv", "-m", "static", "-i", "69.69.69.69"})
                    .WithExample(new []{"ingress-urls.csv", "-m", "dynamic", "-d", "8.8.8.8"});
                
                config.AddCommand<VerifyDnsCutoverPipeline>("dns-propagation")
                    .WithDescription("Check DNS propagation after Ingress configuration")
                    .WithExample(new []{"domains.csv", "-c", "vsan-cname.playcourt.id"})
                    .WithExample(new []{"domains.csv", "-a", "69.69.69.69"});

                config.AddCommand<GetMongoDbActiveConnectionPipeline>("mongo-connection")
                    .WithDescription("Get MongoDB active connection")
                    .WithExample(new []{"logee-prod", "-p", "mongo"});

                config.AddCommand<CalculateMongoDbSizePipeline>("mongo-size")
                    .WithDescription("Calculate MongoDB database size")
                    .WithExample(new []{"logee-prod", "mongos.csv", "-p", "mongo"});

                config.AddCommand<DumpMongoDbPipeline>("mongo-dump")
                    .WithDescription("Dump all MongoDB database")
                    .WithExample(new []{"logee-prod", @"D:\backup", "-p", "mongo"});
            });

            AnsiConsole.Render(new FigletText("MigrasiLogee").LeftAligned().Color(Color.DarkOrange3_1));
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("MigrasiLogee - [red]MIGR[/]asi is pa[red]IN[/].");
            AnsiConsole.WriteLine();

            return app.Run(args);
        }
    }
}

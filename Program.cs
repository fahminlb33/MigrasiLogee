using System.Diagnostics;
using MigrasiLogee.Pipelines;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MigrasiLogee
{
    class Program
    {
        static int Main(string[] args)
        {
            Debugger.Launch();

            var app = new CommandApp();
            app.Configure(config =>
            {
                config.SetApplicationName("migrain");
                config.AddCommand<ScalePodsPipeline>("scale")
                    .WithDescription("Scales Kubernetes deployment replicas");
                config.AddCommand<VerifyServiceUptimePipeline>("uptime")
                    .WithDescription("Check if a service is accessible from internet using public URI");
                config.AddCommand<VerifyDnsCutoverPipeline>("dns-propagation")
                    .WithDescription("Check DNS propagation after Ingress configuration");
                config.AddCommand<GetMongoDbActiveConnectionPipeline>("mongo-connection")
                    .WithDescription("Get MongoDB active connection");
                config.AddCommand<CalculateMongoDbSizePipeline>("mongo-size")
                    .WithDescription("Calculate MongoDB database size");
                config.AddCommand<DumpMongoDbPipeline>("mongo-dump")
                    .WithDescription("Dump all MongoDB database");
            });

            AnsiConsole.Render(new FigletText("MigrasiLogee").LeftAligned().Color(Color.DarkOrange3_1));
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("MigrasiLogee - [red]MIGR[/]asi is pa[red]IN[/].");
            AnsiConsole.WriteLine();

            return app.Run(args);
        }
    }
}

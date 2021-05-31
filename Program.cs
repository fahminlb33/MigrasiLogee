using MigrasiLogee.Pipelines;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MigrasiLogee
{
    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.AddCommand<VerifyServiceUptimePipeline>("uptime");
            });

            AnsiConsole.Render(new FigletText("MigrasiLogee").LeftAligned().Color(Color.DarkOrange3_1));
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("MigrasiLogee - migrasi is pain.");
            AnsiConsole.WriteLine();

            return app.Run(args);
        }
    }
}

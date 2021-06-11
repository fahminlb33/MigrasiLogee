using Spectre.Console;

namespace MigrasiLogee.Helpers
{
    public static class MessageWriter
    {
        public static void ArgumentNotSpecifiedMessage(string optionName)
        {
            AnsiConsole.MarkupLine($"[yellow]{optionName} is not specified.[/]");
        }

        public static void ExecutableNotFoundMessage(string executableName, string optionName)
        {
            AnsiConsole.MarkupLine(
                $"[red]'{executableName}' not found! " +
                $"Add '{executableName}' to your PATH or " +
                $"specify the file using {optionName} option.[/]");
        }
    }
}

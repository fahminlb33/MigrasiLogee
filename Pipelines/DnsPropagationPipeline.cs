﻿using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using CsvHelper;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using MigrasiLogee.Models;
using MigrasiLogee.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MigrasiLogee.Pipelines
{
    public class VerifyDnsCutoverSettings : CommandSettings
    {
        [CommandArgument(0, "<URL_FILE>")]
        [Description("CSV file containing hostname and path to be checked")]
        public string UrlFile { get; set; }

        [CommandOption("-d|--dns <DNS>")]
        [DefaultValue(NetworkHelpers.DefaultDnsResolverAddress)]
        [Description("DNS server used to resolve the host, defaults to 8.8.8.8 if not specified")]
        public string DnsAddress { get; set; }

        [CommandOption("-c|--cname <HOSTNAME>")]
        [Description("Match the CNAME of the answer section from '" + DigClient.DigExecutableName + "'")]
        public string CnameAddress { get; set; }

        [CommandOption("-a|--arec <HOSTNAME>")]
        [Description("Match the A of the answer section from '" + DigClient.DigExecutableName + "'")]
        public string AAddress { get; set; }

        [CommandOption("-i|--dig <CURL_PATH>")]
        [Description("Relative/full path to '" + DigClient.DigExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string DigPath { get; set; }
    }

    public class DnsPropagationPipeline : PipelineBase<VerifyDnsCutoverSettings>
    {
        private readonly DigClient _digClient = new();

        protected override bool ValidateState(CommandContext context, VerifyDnsCutoverSettings settings)
        {
            var digPath = DependencyLocator.WhereExecutable(settings.DigPath, DigClient.DigExecutableName);
            if (digPath == null)
            {
                MessageWriter.ExecutableNotFoundMessage(DigClient.DigExecutableName, "--dig");
                return false;
            }

            _digClient.DigExecutablePath = digPath;

            if (string.IsNullOrWhiteSpace(settings.AAddress) && string.IsNullOrWhiteSpace(settings.CnameAddress))
            {
                AnsiConsole.MarkupLine("[red]A record or CNAME record must be specified.[/]");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(settings.AAddress) && !IPAddress.TryParse(settings.AAddress, out var _))
            {
                AnsiConsole.MarkupLine("[red]The specified IP in A record is invalid.[/]");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(settings.CnameAddress))
            {
                AnsiConsole.MarkupLine("[red]The specified IP in A record is invalid.[/]");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(settings.DnsAddress) && !IPAddress.TryParse(settings.DnsAddress, out var _))
            {
                AnsiConsole.MarkupLine("[yellow]DNS IP is not specified using --dns or invalid IP is entered. Using 8.8.8.8 as DNS resolver.[/]");
            }

            _digClient.DnsAddress = IPAddress.TryParse(settings.DnsAddress, out var _) ? settings.DnsAddress : NetworkHelpers.DefaultDnsResolverAddress;

            return true;
        }

        protected override void PreRun(CommandContext context, VerifyDnsCutoverSettings settings)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Render(new Text("{ DNS Propagation }").Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            AnsiConsole.WriteLine("DNS resolver  : {0}", _digClient.DnsAddress);
            AnsiConsole.WriteLine();
        }

        protected override int Run(CommandContext context, VerifyDnsCutoverSettings settings)
        {
            using var reader = new StreamReader(settings.UrlFile);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            var records = csv.GetRecords<DnsCutoverRecord>().ToList();
            var table = new Table().LeftAligned();

            AnsiConsole.Live(table)
                .Overflow(VerticalOverflow.Ellipsis)
                .Start(ctx =>
                {
                    table.AddColumn("Host");
                    table.AddColumn("Resolved IP");
                    table.AddColumn("TTL");
                    table.AddColumn("Propagated?");
                    table.AddColumn("Ingress");
                    ctx.Refresh();

                    foreach (var entry in records)
                    {
                        var result = _digClient.ResolveDnsPropagation(entry.Hostname);
                        var record = result.Records.LastOrDefault();

                        if (record == null)
                        {
                            table.AddRow(
                                result.Host.TrimLength(20),
                                "No answer from NS",
                                "",
                                "[red]No[/]",
                                entry.IngressName);
                        }
                        else
                        {
                            var propagated = GetPropagationStatus(result, settings);
                            var propagationMarkup = propagated ? "[green]Yes[/]" : "[red]No[/]";

                            table.AddRow(
                                result.Host.TrimLength(20),
                                record.Destination,
                                record.Ttl.ToString(),
                                propagationMarkup,
                                entry.IngressName);
                        }

                        ctx.Refresh();
                    }
                });

            return 0;
        }

        private static bool GetPropagationStatus(DnsPropagation propagation, VerifyDnsCutoverSettings setting)
        {
            return propagation.Records.Any(x =>
            {
                if (!string.IsNullOrWhiteSpace(setting.AAddress))
                {
                    return x.Destination == setting.AAddress;
                }

                return x.Destination.Contains(setting.CnameAddress);
            });
        }
    }
}
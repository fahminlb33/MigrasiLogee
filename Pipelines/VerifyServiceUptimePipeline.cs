using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using CsvHelper;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using MigrasiLogee.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MigrasiLogee.Pipelines
{
    public class IngressServiceUptimeSettings : CommandSettings
    {
        [CommandArgument(0, "<URL_FILE>")]
        [Description("CSV file containing hostname and path to be checked")]
        public string UrlFile { get; set; }

        [CommandOption("-m|--mode <MODE>")]
        [DefaultValue("dynamic")]
        [Description("IP resolution mode, either static or dynamic, defaults to dynamic")]
        public string Mode { get; set; }

        [CommandOption("-i|--ip <IP>")]
        [Description("Use this IP and ignore DNS resolution to point the server")]
        public string StaticServerAddress { get; set; }

        [CommandOption("-d|--dns <DNS>")]
        [Description("DNS server used to resolve the host, defaults to 8.8.8.8 if not specified")]
        public string DnsAddress { get; set; }

        [CommandOption("-c|--curl <CURL_PATH>")]
        [Description("Relative/full path to curl executable (or just name if it's in PATH)")]
        public string CurlPath { get; set; }
    }

    public class VerifyServiceUptimePipeline : PipelineBase<IngressServiceUptimeSettings>
    {
        private readonly IngressChecker _ingress = new();

        protected override bool ValidateState(CommandContext context, IngressServiceUptimeSettings settings)
        {
            var curlPath = DependencyLocator.WhereCurl(settings.CurlPath);
            if (curlPath == null)
            {
                AnsiConsole.MarkupLine("[red]cURL not found! Add curl to your PATH or specify the path using --curl option.[/]");
                return false;
            }

            if (!DependencyLocator.IsCurlSupported(curlPath))
            {
                AnsiConsole.MarkupLine($"[red]{curlPath}[/]");
                AnsiConsole.MarkupLine("[red]Current cURL version is not supported, please update your cURL (--dns-server feature is not available)[/]");
                return false;
            }

            _ingress.CurlExecutablePath = curlPath;

            if (!DependencyLocator.IsFileExists(settings.UrlFile))
            {
                AnsiConsole.MarkupLine("[red]The specified input file is not found.[/]");
                return false;
            }

            if (settings.Mode == "static")
            {
                if (string.IsNullOrEmpty(settings.StaticServerAddress) || !IPAddress.TryParse(settings.StaticServerAddress, out var _))
                {
                    AnsiConsole.MarkupLine("[red]Static IP is not specified using --ip or invalid IP is entered.[/]");
                    return false;
                }

                _ingress.UseDnsResolver = false;
                _ingress.StaticServerAddress = settings.StaticServerAddress;
            }
            else if  (settings.Mode == "dynamic")
            {
                if (string.IsNullOrEmpty(settings.DnsAddress) || !IPAddress.TryParse(settings.DnsAddress, out var _))
                {
                    AnsiConsole.MarkupLine("[yellow]DNS IP is not specified using --dns or invalid IP is entered. Using 8.8.8.8 as DNS resolver.[/]");
                }

                _ingress.UseDnsResolver = true;
                _ingress.DnsAddress = IPAddress.TryParse(settings.DnsAddress, out var _) ? settings.DnsAddress : IngressChecker.DefaultDnsResolverAddress;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Unknown mode.[/]");
                return false;
            }

            return true;
        }

        protected override void PreRun(CommandContext context, IngressServiceUptimeSettings settings)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Render(new Text("{ Ingress Route Checker }").Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            AnsiConsole.WriteLine("Use DNS resolver  : {0}", _ingress.UseDnsResolver);
            AnsiConsole.WriteLine("DNS resolver      : {0}", _ingress.DnsAddress);
            AnsiConsole.WriteLine("Static server IP  : {0}", _ingress.StaticServerAddress);
        }

        protected override int Run(CommandContext context, IngressServiceUptimeSettings settings)
        {
            using var reader = new StreamReader(settings.UrlFile);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            //csv.Context.RegisterClassMap<CsvBooleanConverter>();

            var records = csv.GetRecords<IngressCheckEntry>();
   
            var table = new Table().LeftAligned();
            AnsiConsole.Live(table)
                .Overflow(VerticalOverflow.Ellipsis)
                .Start(ctx =>
                {
                    table.AddColumn("Host");
                    table.AddColumn("Resolved IP");
                    table.AddColumn("Port");
                    table.AddColumn("Path");
                    table.AddColumn("SSL");
                    table.AddColumn("HTTP");
                    table.AddColumn("Response Body");
                    table.AddColumn("Ingress");
                    ctx.Refresh();

                    foreach (var entry in records)
                    {
                        var result = _ingress.IsServiceUp(entry);
                        var ipMarkup = result.IP.Contains("not resolve")
                            ? $"[red]{result.IP}[/]"
                            : result.IP;
                        var sslMarkup = result.SslStatus.Contains("problem") || result.SslStatus.Contains("No SSL")
                            ? $"[red]{result.SslStatus}[/]"
                            : result.SslStatus;
                        var httpMarkup = result.HttpCode.Contains("200")
                            ? $"[green]{result.HttpCode}[/]"
                            : $"[red]{result.HttpCode}[/]";

                        table.AddRow(result.Host.TrimLength(20), ipMarkup, result.Port.ToString(), result.Path, sslMarkup, httpMarkup, result.Body.Replace(Environment.NewLine, "").TrimLength(), result.Ingress);
                        ctx.Refresh();
                    }
                });

            return 0;
        }
    }
}

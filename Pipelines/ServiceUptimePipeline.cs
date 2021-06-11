using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
    public class ServiceUptimeSettings : CommandSettings
    {
        [CommandArgument(0, "<URL_FILE>")]
        [Description("CSV file containing hostname and path to be checked")]
        public string UrlFile { get; set; }

        [CommandOption("-m|--mode <MODE>")]
        [DefaultValue("dynamic")]
        [Description("IP resolution mode, either 'static' or 'dynamic', defaults to dynamic")]
        public string Mode { get; set; }

        [CommandOption("-i|--ip <IP>")]
        [Description("Use this IP and ignore DNS resolution to point the server")]
        public string StaticServerIp { get; set; }

        [CommandOption("-d|--dns <DNS>")]
        [Description("DNS server used to resolve the host, defaults to 8.8.8.8 if not specified")]
        [DefaultValue(NetworkHelpers.DefaultDnsResolverAddress)]
        public string DnsAddress { get; set; }

        [CommandOption("-c|--curl <CURL_PATH>")]
        [Description("Relative/full path to '" + CurlClient.CurlExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string CurlPath { get; set; }
    }

    public class ServiceUptimePipeline : PipelineBase<ServiceUptimeSettings>
    {
        private readonly CurlClient _curl = new();

        protected override bool ValidateState(CommandContext context, ServiceUptimeSettings settings)
        {
            var curlPath = DependencyLocator.WhereExecutable(settings.CurlPath, CurlClient.CurlExecutableName);
            if (curlPath == null)
            {
                MessageWriter.ExecutableNotFoundMessage(CurlClient.CurlExecutableName, "--curl");
                return false;
            }

            _curl.CurlExecutablePath = curlPath;

            if (!_curl.IsCurlSupported())
            {
                AnsiConsole.MarkupLine($"[red]{curlPath}[/]");
                AnsiConsole.MarkupLine("[red]Current cURL version is not supported, please update your cURL (--dns-server feature is not available)[/]");
                return false;
            }

            if (!DependencyLocator.IsFileExists(settings.UrlFile))
            {
                AnsiConsole.MarkupLine("[red]The specified input file is not found.[/]");
                return false;
            }

            if (settings.Mode == "static")
            {
                if (string.IsNullOrEmpty(settings.StaticServerIp) || !IPAddress.TryParse(settings.StaticServerIp, out var _))
                {
                    AnsiConsole.MarkupLine("[red]Static IP is not specified using --ip or invalid IP is entered.[/]");
                    return false;
                }

                _curl.UseDnsResolver = false;
                _curl.StaticServerIp = settings.StaticServerIp;
            }
            else if (settings.Mode == "dynamic")
            {
                if (string.IsNullOrEmpty(settings.DnsAddress) || !IPAddress.TryParse(settings.DnsAddress, out var _))
                {
                    AnsiConsole.MarkupLine("[yellow]DNS IP is not specified using --dns or invalid IP is entered. Using 8.8.8.8 as DNS resolver.[/]");
                }

                _curl.UseDnsResolver = true;
                _curl.DnsAddress = IPAddress.TryParse(settings.DnsAddress, out var _) ? settings.DnsAddress : NetworkHelpers.DefaultDnsResolverAddress;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Unknown mode.[/]");
                return false;
            }

            return true;
        }

        protected override void PreRun(CommandContext context, ServiceUptimeSettings settings)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Render(new Text("{ Service Uptime }").Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            AnsiConsole.WriteLine("Use DNS resolver  : {0}", _curl.UseDnsResolver);
            AnsiConsole.WriteLine("DNS resolver      : {0}", _curl.DnsAddress);
            AnsiConsole.WriteLine("Static server IP  : {0}", _curl.StaticServerIp);
        }

        protected override int Run(CommandContext context, ServiceUptimeSettings settings)
        {
            using var reader = new StreamReader(settings.UrlFile);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            var records = csv.GetRecords<ServiceUptimeRecord>();
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
                        var result = _curl.GetServiceUptime(new ServiceInfo(entry.UseHttps, entry.HostName, entry.Path));

                        var ipMarkup = result.Ip.Contains("not resolve")
                            ? $"[red]{result.Ip}[/]"
                            : result.Ip;
                        var sslMarkup = result.SslStatus.Contains("problem") || result.SslStatus.Contains("No SSL")
                            ? $"[red]{result.SslStatus}[/]"
                            : result.SslStatus;
                        var httpMarkup = result.HttpCode.Contains("200")
                            ? $"[green]{result.HttpCode}[/]"
                            : $"[red]{result.HttpCode}[/]";

                        table.AddRow(result.Host.TrimLength(20),
                            ipMarkup,
                            result.Port.ToString(),
                            result.Path, sslMarkup,
                            httpMarkup,
                            result.Body.Replace(Environment.NewLine, "").TrimLength(),
                            $"{entry.IngressName} ({entry.ProjectName})");
                        ctx.Refresh();
                    }
                });

            return 0;
        }
    }
}

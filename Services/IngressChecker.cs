using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;

namespace MigrasiLogee.Services
{
    public record IngressCheckEntry(bool UseHttps, string HostName, string Path, string ProjectName, string IngressName);
    public record ResolvedHostMatch(string Host, string IP, string Path, int Port, string SslStatus, string HttpCode, string Body, string Ingress);

    public class IngressChecker
    {
        public const string DefaultDnsResolverAddress = "8.8.8.8";
        
        private const int HttpPort = 80;
        private const int HttpsPort = 443;

        private readonly Regex _httpCodeRegex;
        private readonly Regex _hostResolveRegex;

        public bool UseDnsResolver { get; set; }
        public string StaticServerAddress { get; set; } 
        public string DnsAddress { get; set; } 
        public string DigExecutablePath { get; set; }
        public string CurlExecutablePath { get; set; }

        public IngressChecker()
        {
            _httpCodeRegex = new Regex(@"[0-9]{3}", RegexOptions.Compiled);
            _hostResolveRegex = new Regex(@"(?<host>[a-zA-Z0-9\.-]*) \((?<ip>[0-9\.]+)\) port (?<port>[0-9]{2,3})",
                RegexOptions.Compiled);
        }

        public void IsDnsPropagated(IngressCheckEntry entry)
        {
            using var process = new ProcessJob
            {
                ExecutableName = DigExecutablePath,
                Arguments = $"@{DnsAddress} {entry.HostName}"
            };

            var result = process.StartWaitWithRedirect();
            Console.WriteLine(result.StandardError);
            Console.WriteLine(result.StandardOutput);
            Console.WriteLine(result.ExitCode);
            Console.WriteLine("====================================");
        }

        public ResolvedHostMatch IsServiceUp(IngressCheckEntry entry)
        {
            var port = entry.UseHttps ? HttpsPort : HttpPort;
            var schema = entry.UseHttps ? "https" : "http";
            var uriBuilder = new UriBuilder(schema, entry.HostName, port,  entry.Path);
            var resolver = !UseDnsResolver
                ? $"--resolve {entry.HostName}:{port}:{StaticServerAddress}"
                : $"--dns-servers {DnsAddress}";

            using var process = new ProcessJob
            {
                ExecutableName = CurlExecutablePath,
                Arguments = $"-Lvs --no-sessionid {resolver} {uriBuilder}"
            };

            Debug.Print(process.Arguments);
            
            var result = process.StartWaitWithRedirect();
            return ParseCurl(result.StandardError, result.StandardOutput, entry);
        }

        private ResolvedHostMatch ParseCurl(string headers, string body, IngressCheckEntry entry)
        {
            var headersArray = headers.Split(Environment.NewLine);

            try
            {
                var sslMatch = headersArray.FirstOrDefault(x => x.Contains("*  SSL cert"))?[3..] ?? "No SSL";
                var hostResolveMatch = _hostResolveRegex.Match(headersArray.First(x => x.Contains("Connected to")));
                var httpCodeMatch  = _httpCodeRegex.Match(headersArray.First(x => x.Contains("< HTTP/")));

                return new ResolvedHostMatch(
                    hostResolveMatch.Groups["host"].Value, 
                    hostResolveMatch.Groups["ip"].Value,
                    entry.Path,
                    int.Parse(hostResolveMatch.Groups["port"].Value),
                    sslMatch,
                    httpCodeMatch.Value,
                    body,
                    $"{entry.IngressName} ({entry.ProjectName})");
            }
            catch (Exception)
            {
                return new ResolvedHostMatch(
                    entry.HostName,
                    "Could not resolve host",
                    entry.Path,
                    0,
                    "",
                    "",
                    "",
                    $"{entry.IngressName} ({entry.ProjectName})");
            }
        }
    }
}
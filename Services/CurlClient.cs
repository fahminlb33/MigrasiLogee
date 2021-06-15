using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;

namespace MigrasiLogee.Services
{
    public record ServiceInfo(bool UseHttps, string HostName, string Path);

    public record ServiceUptime(string Host, string Ip, string Path, int Port, string SslStatus, string HttpCode, string Body);

    public class CurlClient
    {
        public const string CurlExecutableName = "curl";

        private readonly Regex _httpCodeRegex;
        private readonly Regex _hostResolveRegex;

        public bool UseDnsResolver { get; set; }
        public string DnsAddress { get; set; }
        public string StaticServerIp { get; set; } 
        public string CurlExecutablePath { get; set; }

        public CurlClient()
        {
            _httpCodeRegex = new Regex(@"[0-9]{3}", RegexOptions.Compiled);
            _hostResolveRegex = new Regex(@"(?<host>[a-zA-Z0-9\.-]*) \((?<ip>[0-9\.]+)\) port (?<port>[0-9]{2,3})",
                RegexOptions.Compiled);
        }

        public bool IsCurlSupported()
        {
            using var job = new ProcessJob
            {
                ExecutableName = CurlExecutablePath,
                Arguments = "--dns-servers 8.8.8.8 google.com"
            };

            var result = job.StartWaitWithRedirect();
            return result.ExitCode == 0;
        }

        public ServiceUptime GetServiceUptime(ServiceInfo info)
        {
            var port = info.UseHttps ? NetworkHelpers.HttpsPort : NetworkHelpers.HttpPort;
            var schema = info.UseHttps ? "https" : "http";
            var uriBuilder = new UriBuilder(schema, info.HostName, port,  info.Path);
            var resolver = !UseDnsResolver
                ? $"--resolve {info.HostName}:{port}:{StaticServerIp}"
                : $"--dns-servers {DnsAddress}";

            using var process = new ProcessJob
            {
                ExecutableName = CurlExecutablePath,
                Arguments = $"-Lvs --no-sessionid {resolver} {uriBuilder}"
            };

            Debug.Print(process.Arguments);
            
            var result = process.StartWaitWithRedirect();
            var headersArray = result.StandardError.Split(Environment.NewLine);

            try
            {
                var sslMatch = headersArray.FirstOrDefault(x => x.Contains("*  SSL cert"))?[3..] ?? "No SSL";
                var hostResolveMatch = _hostResolveRegex.Match(headersArray.First(x => x.Contains("Connected to")));
                var httpCodeMatch  = _httpCodeRegex.Match(headersArray.First(x => x.Contains("< HTTP/")));

                return new ServiceUptime(
                    hostResolveMatch.Groups["host"].Value, 
                    hostResolveMatch.Groups["ip"].Value,
                    info.Path,
                    int.Parse(hostResolveMatch.Groups["port"].Value),
                    sslMatch,
                    httpCodeMatch.Value,
                    result.StandardOutput);
            }
            catch (Exception)
            {
                return new ServiceUptime(
                    info.HostName,
                    "Can't resolve host",
                    info.Path,
                    0,
                    "",
                    "",
                    "");
            }
        }
    }
}
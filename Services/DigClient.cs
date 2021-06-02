using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MigrasiLogee.Infrastructure;

namespace MigrasiLogee.Services
{
    public record ResolvedDnsRecord(string Source, int Ttl, string Direction, string RecordType, string Destination);

    public record DnsPropagation(string Host, IEnumerable<ResolvedDnsRecord> Records);

    public class DigClient
    {
        public string DnsAddress { get; set; }
        public string DigExecutablePath { get; set; }

        private readonly Regex _answerSectionRegex;

        public DigClient()
        {
            _answerSectionRegex =
                new Regex(
                    @"(?<source>[a-zA-Z0-9\.-]*)\s(?<ttl>[0-9]{1,10})\s(?<dir>[A-Z]*)\s(?<type>[A-Z]*)\s(?<target>[a-zA-Z0-9\.-]*)",
                    RegexOptions.Compiled);
        }

        public DnsPropagation ResolveDnsPropagation(string host)
        {
            using var process = new ProcessJob
            {
                ExecutableName = DigExecutablePath,
                Arguments = $"\\@{DnsAddress} {host}"
            };

            Debug.Print(process.Arguments);

            var result = process.StartWaitWithRedirect();
            var records = result.StandardOutput
                .Split(Environment.NewLine)
                .SkipWhile(x => !x.Contains("ANSWER SECTION"))
                .TakeWhile(x => !x.Contains("Query time"))
                .Skip(1)
                .SkipLast(1)
                .Select(x =>
                {
                    var matched = _answerSectionRegex.Match(x);
                    return new ResolvedDnsRecord(
                        matched.Groups["source"].Value, 
                        int.Parse(matched.Groups["ttl"].Value),
                        matched.Groups["dir"].Value, 
                        matched.Groups["type"].Value, 
                        matched.Groups["target"].Value);
                })
                .ToList();

            return new DnsPropagation(host, records);
        }
    }
}

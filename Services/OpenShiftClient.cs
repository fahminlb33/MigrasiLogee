using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MigrasiLogee.Exceptions;
using MigrasiLogee.Infrastructure;
using Newtonsoft.Json.Linq;

namespace MigrasiLogee.Services
{
    public class OpenShiftClient
    {
        public const string OcExecutableName = "oc";

        public string OcExecutable { get; set; }
        public string ProjectName { get; set; }

        public IEnumerable<string> GetPodNames()
        {
            using var process = new ProcessJob
            {
                ExecutableName = OcExecutable,
                Arguments = $"get pods -o name -n {ProjectName}"
            };

            var (output, error, _) = process.StartWaitWithRedirect();
            ValidateOutput(error);

            return output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizePodName);
        }

        public bool Scale(string deployment, int replicas)
        {
            using var process = new ProcessJob
            {
                ExecutableName = OcExecutable,
                Arguments = $"oc scale --replicas={replicas} dc {deployment}"
            };

            var (output, error, _) = process.StartWaitWithRedirect();
            ValidateOutput(error);

            return output.Contains("scaled");
        }

        public ProcessJob PortForward(string podName, int localPort, int remotePort)
        {
            var process = new ProcessJob
            {
                ExecutableName = OcExecutable,
                Arguments = $"port-forward {podName} {localPort}:{remotePort} -n {ProjectName}"
            };

            process.StartJob();

            Thread.Sleep(2000);
            process.EnsureStarted();

            return process;
        }

        public IEnumerable<string> GetSecretNames()
        {
            using var process = new ProcessJob
            {
                ExecutableName = OcExecutable,
                Arguments = $"get secrets -o name -n {ProjectName}"
            };

            var (output, error, _) = process.StartWaitWithRedirect();
            ValidateOutput(error);

            return output.Split(Environment.NewLine).Where(x => !string.IsNullOrWhiteSpace(x));
        }

        public Dictionary<string, string> GetSecret(string secretName)
        {
            using var process = new ProcessJob
            {
                ExecutableName = OcExecutable,
                Arguments = $"get {secretName} -o json -n {ProjectName}"
            };

            var (output, error, _) = process.StartWaitWithRedirect();
            ValidateOutput(error);

            return JToken.Parse(output)["data"]
                ?.ToObject<Dictionary<string, string>>()
                ?.ToDictionary(x => x.Key, y => Encoding.UTF8.GetString(Convert.FromBase64String(y.Value)));
        }

        public static string NormalizePodName(string podName)
        {
            return podName.Remove(0, 5);
        }

        public static string PodToServiceName(string normalizedPodName)
        {
            return normalizedPodName
                .Split('-')
                .SkipLast(2)
                .Aggregate("", (current, next) => current + "-" + next)
                .Remove(0, 1);
        }

        private static void ValidateOutput(string output)
        {
            if (output.Contains("Error from server"))
            {
                throw new OpenShiftException(output);
            }
        }
    }
}

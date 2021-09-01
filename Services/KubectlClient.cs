using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MigrasiLogee.Exceptions;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MigrasiLogee.Services
{
    public class KubectlClient
    {
        public const string KubectlExecutableName = "kubectl";

        public string KubectlExecutable { get; set; }
        public string KubeconfigFilePath { get; set; }
        public string NamespaceName { get; set; }

        public IEnumerable<string> GetDeploymentNames()
        {
            using var process = new ProcessJob
            {
                ExecutableName = KubectlExecutable,
                Arguments = $"--kubeconfig \"{KubeconfigFilePath}\" -n {NamespaceName} get deployment -o name"
            };

            var (output, error, _) = process.StartWaitWithRedirect();
            ValidateOutput(error);

            return output.Split(StringHelpers.NewlineCharacters, StringSplitOptions.None)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(StringHelpers.NormalizeKubeResourceName)
                .ToList();
        }

        public bool Scale(string deployment, int replicas)
        {
            using var process = new ProcessJob
            {
                ExecutableName = KubectlExecutable,
                Arguments = $"--kubeconfig \"{KubeconfigFilePath}\" -n {NamespaceName} scale --replicas={replicas} deployment/{deployment}"
            };

            var (output, error, _) = process.StartWaitWithRedirect();
            ValidateOutput(error);

            return output.Contains("scaled");
        }

        public ProcessJob PortForward(string podName, int localPort, int remotePort)
        {
            var process = new ProcessJob
            {
                ExecutableName = KubectlExecutable,
                Arguments = $"--kubeconfig \"{KubeconfigFilePath}\" -n {NamespaceName} port-forward {podName} {localPort}:{remotePort}"
            };

            process.StartJob();

            Thread.Sleep(2000);
            process.EnsureStarted();

            return process;
        }

        public Dictionary<string, string> GetSecret(string secretName)
        {
            using var process = new ProcessJob
            {
                ExecutableName = KubectlExecutable,
                Arguments = $"--kubeconfig \"{KubeconfigFilePath}\" -n {NamespaceName} get secret {secretName} -o json"
            };

            var (output, error, _) = process.StartWaitWithRedirect();
            ValidateOutput(error);

            return JToken.Parse(output)["data"]
                ?.ToObject<Dictionary<string, string>>()
                ?.ToDictionary(x => x.Key, y => Encoding.UTF8.GetString(Convert.FromBase64String(y.Value)));
        }

        public bool UpdateSecret(string secretName, IDictionary<string, string> data)
        {
            using var process = new ProcessJob
            {
                ExecutableName = KubectlExecutable,
                Arguments = $"--kubeconfig \"{KubeconfigFilePath}\" -n {NamespaceName} get secret {secretName} -o json"
            };

            var (output, error, _) = process.StartWaitWithRedirect();
            ValidateOutput(error);

            var secret = JToken.Parse(output);
            foreach (var (key, value) in data)
            {
                // ReSharper disable once PossibleNullReferenceException
                secret["data"][key] = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            }

            var tempConfigFile = Path.ChangeExtension(Path.GetTempFileName(), "json");
            File.WriteAllText(tempConfigFile, JsonConvert.SerializeObject(secret));

            using var process2 = new ProcessJob
            {
                ExecutableName = KubectlExecutable,
                Arguments = $"--kubeconfig \"{KubeconfigFilePath}\" -n {NamespaceName} apply -f \"{tempConfigFile}\""
            };

            var (output2, error2, exitCode) = process2.StartWaitWithRedirect();
            if (exitCode != 0)
            {
                Console.WriteLine(error2);
            }

            return exitCode == 0;
        }

        private static void ValidateOutput(string output)
        {
            if (output.Contains("error:"))
            {
                throw new KubectlException(output);
            }
        }
    }
}

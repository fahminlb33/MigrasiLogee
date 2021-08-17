using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MigrasiLogee.Exceptions;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;

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

        private static void ValidateOutput(string output)
        {
            if (output.Contains("error:"))
            {
                throw new KubectlException(output);
            }
        }
    }
}

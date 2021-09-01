using System.ComponentModel;
using System.Threading;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using MigrasiLogee.Models;
using MigrasiLogee.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MigrasiLogee.Pipelines
{
    public class ChangeMongoPrimaryForwarderSettings  : CommandSettings
    {
        [CommandArgument(0, "<NAMESPACE>")]
        [Description("Namespace/project name containing mongo forwarder deployment")]
        public string NamespaceName { get; set; }

        [CommandArgument(1, "[DEPLOYMENT]")]
        [DefaultValue("mongo-forwarder")]
        [Description("Deployment name for mongo forwarder in Kubernetes cluster")]
        public string DeploymentName { get; set; }

        [CommandOption("--mongo <MONGO_PATH>")]
        [Description("Relative/full path to '" + MongoClient.MongoExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string MongoPath { get; set; }

        [CommandOption("-f|--kubeconfig <KUBECONFIG_FILE>")]
        [Description("Kubeconfig .yml file to specify Kubernetes login")]
        public string KubeconfigFile { get; set; }

        [CommandOption("--kubectl <KUBECTL_PATH>")]
        [Description("Relative/full path to '" + KubectlClient.KubectlExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string KubectlPath { get; set; }
    }

    public class ChangeMongoPrimaryForwarderPipeline: PipelineBase<ChangeMongoPrimaryForwarderSettings>
    {
        private readonly KubectlClient _kubectl = new();
        private readonly MongoClient _mongo = new();

        protected override bool ValidateState(CommandContext context, ChangeMongoPrimaryForwarderSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.NamespaceName))
            {
                MessageWriter.ArgumentNotSpecifiedMessage("<NAMESPACE>");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.DeploymentName))
            {
                MessageWriter.ArgumentNotSpecifiedMessage("<DEPLOYMENT>");
                return false;
            }

            var mongoPath = DependencyLocator.WhereExecutable(settings.MongoPath, MongoClient.MongoExecutableName);
            if (mongoPath == null)
            {
                MessageWriter.ExecutableNotFoundMessage(MongoClient.MongoExecutableName, "--mongo");
                return false;
            }
            
            var kubectlPath = DependencyLocator.WhereExecutable(settings.KubectlPath, KubectlClient.KubectlExecutableName);
            if (kubectlPath == null)
            {
                MessageWriter.ExecutableNotFoundMessage(KubectlClient.KubectlExecutableName, "--kubectl");
                return false;
            }

            if (!DependencyLocator.IsFileExists(settings.KubeconfigFile))
            {
                AnsiConsole.MarkupLine("[red]Kubeconfig file not found! Specify the path using --kubeconfig option.[/]");
                return false;
            }

            _kubectl.KubectlExecutable = kubectlPath;
            _kubectl.KubeconfigFilePath = settings.KubeconfigFile;
            _kubectl.NamespaceName = settings.NamespaceName;

            _mongo.MongoExecutable = mongoPath;
            return true;
        }

        protected override int Run(CommandContext context, ChangeMongoPrimaryForwarderSettings settings)
        {
            // port-forward to existing deployment
            AnsiConsole.WriteLine("Port-forward to mongo forwarder...");
            using var job =_kubectl.PortForward($"deployment/{settings.DeploymentName}", NetworkHelpers.LocalMongoPort, NetworkHelpers.RemoteMongoPort);

            // poll for cluster info
            MongoClusterInfo clusterInfo;
            var checkCount = 0;
            do
            {
                clusterInfo = _mongo.GetClusterInfo($"localhost:{NetworkHelpers.LocalMongoPort}");

                if (clusterInfo == null) Thread.Sleep(1000);
                checkCount++;
            } while (clusterInfo == null && checkCount < 3);

            // can't port-forward or not getting the data, exit
            if (clusterInfo == null)
            {
                AnsiConsole.MarkupLine("[red]Can't port-forward or access database.[/]");
                return -1;
            }

            // print current cluster info
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("ReplSet name: {0}", clusterInfo.SetName);
            AnsiConsole.MarkupLine("Is primary?   {0}", clusterInfo.IsMaster ? "[green]Yes[/]" : "[red]No[/]");
            AnsiConsole.WriteLine("Connected to: {0}", clusterInfo.Me);
            AnsiConsole.WriteLine("Master:       {0}", clusterInfo.Primary);
            AnsiConsole.WriteLine();

            // already connected to master, exit
            if (clusterInfo.IsMaster)
            {
                AnsiConsole.WriteLine("Already connected to master, exiting...");
                return 0;
            }

            // update deployment secret
            AnsiConsole.WriteLine("Updating secret...");
            var secret = _kubectl.GetSecret(settings.DeploymentName);
            secret["SOCAT_FORWARD_IP"] = clusterInfo.Primary;

            _kubectl.UpdateSecret(settings.DeploymentName, secret);

            // restart the pod
            AnsiConsole.WriteLine("Restarting pod...");
            _kubectl.Scale(settings.DeploymentName, 0);
            Thread.Sleep(5000);
            _kubectl.Scale(settings.DeploymentName, 1);

            AnsiConsole.WriteLine("Done!");

            return 0;
        }
    }
}

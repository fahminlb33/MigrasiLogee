using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using MigrasiLogee.Models;
using MigrasiLogee.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MigrasiLogee.Pipelines
{
    public class AlterKafkaPartitionSettings : CommandSettings
    {
        [CommandArgument(0, "<NAMESPACE>")]
        [Description("Namespace/project name containing Kafka broker forwarder deployment")]
        public string NamespaceName { get; set; }

        [CommandOption("-d|--pod <POD>")]
        [Description("Pod name for Kafka broker forwarder in Kubernetes cluster")]
        public string PodName { get; set; }

        [DefaultValue(9094)]
        [CommandOption("-p|--port <PORT>")]
        [Description("Forwarded port from Kafka broker forwarder in Kubernetes cluster")]
        public int Port { get; set; }

        [CommandOption("-s|--save <FILE_PATH>")]
        [Description("Save topics list that's been used in this pipeline")]
        public string TopicsOutputPath { get; set; }

        [CommandOption("-f|--kubeconfig <KUBECONFIG_FILE>")]
        [Description("Kubeconfig .yml file to specify Kubernetes login")]
        public string KubeconfigFile { get; set; }

        [CommandOption("--kubectl <KUBECTL_PATH>")]
        [Description("Relative/full path to '" + KubectlClient.KubectlExecutableName + "' executable (or leave empty if it's in PATH)")]
        public string KubectlPath { get; set; }

        [CommandOption("-c|--command-config <KAFKA_COMMAND_CONFIG_PATH>")]
        [Description("Relative/full path to Kafka command config .conf file")]
        public string KafkaCommandConfigPath { get; set; }

        [CommandOption("--kafka <KAFKA_SCRIPTS_PATH>")]
        [Description("Relative/full path to Kafka scripts directory (or leave empty if it's in PATH)")]
        public string KafkaPath { get; set; }
    }

    public class AlterKafkaPartitionPipeline : PipelineBase<AlterKafkaPartitionSettings>
    {
        private readonly KafkaClient _kafka = new();
        private readonly KubectlClient _kubectl = new();

        protected override bool ValidateState(CommandContext context, AlterKafkaPartitionSettings settings)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                AnsiConsole.MarkupLine("[red]This pipeline currently only supports running on Windows.[/]");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.NamespaceName))
            {
                MessageWriter.ArgumentNotSpecifiedMessage("<NAMESPACE>");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.PodName))
            {
                MessageWriter.ArgumentNotSpecifiedMessage("<POD>");
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

            if (!DependencyLocator.IsDirectoryExists(settings.KafkaPath))
            {
                AnsiConsole.MarkupLine("[red]Kafka directory not found![/]");
                return false;
            }

            _kubectl.NamespaceName = settings.NamespaceName;
            _kubectl.KubectlExecutable = kubectlPath;
            _kubectl.KubeconfigFilePath = settings.KubeconfigFile;

            _kafka.KafkaScriptsDirectory = settings.KafkaPath;
            _kafka.CommandConfigFile = settings.KafkaCommandConfigPath;

            return true;
        }

        protected override int Run(CommandContext context, AlterKafkaPartitionSettings settings)
        {
            AnsiConsole.WriteLine("Port forwarding deployment...");
            _kubectl.PortForward(settings.PodName, settings.Port, settings.Port);

            _kafka.BootstrapServers = new List<BootstrapServer>
            {
                new() {Host = "localhost", Port = settings.Port}
            };

            AnsiConsole.WriteLine("Listing topics...");
            var topics = _kafka.ListTopics().ToList();

            AnsiConsole.WriteLine("Found {0} topics.", topics.Count);
            AnsiConsole.WriteLine("Altering all topics to 2 partitions...");
            foreach (var topic in topics)
            {
                AnsiConsole.Write("Altering topic {0}...", topic);
                _kafka.AlterPartitions(topic, 2);
                AnsiConsole.MarkupLine("[green]DONE![/]");
            }
            
            AnsiConsole.WriteLine("Reassigning topic partitions and replicas...");
            AnsiConsole.WriteLine("Writing manifest...");
            var manifestSavePath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
            Debug.Print(manifestSavePath);
            _kafka.WritePartitionReassignmentManifest(manifestSavePath, topics);

            AnsiConsole.WriteLine("Executing partition reassignment...");
            _kafka.ExecutePartitionReassignment(manifestSavePath);

            AnsiConsole.WriteLine("Kafka reassignment completed.");

            if (!string.IsNullOrEmpty(settings.TopicsOutputPath))
            {
                AnsiConsole.WriteLine("Writing topics to file...");
                File.WriteAllLines(settings.TopicsOutputPath, topics);
            }

            return 0;
        }
    }
}

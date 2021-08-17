using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MigrasiLogee.Infrastructure;
using MigrasiLogee.Models;
using Newtonsoft.Json;

namespace MigrasiLogee.Services
{
    public class KafkaClient
    {
        public string KafkaScriptsDirectory { get; set; }
        public string CommandConfigFile { get; set; }
        public List<BootstrapServer> BootstrapServers { get; set; }

        public IEnumerable<string> ListTopics()
        {
            var path = BuildBaseKafkaCommand("kafka-topics");
            using var process = new ProcessJob
            {
                ExecutableName = "cmd.exe",
                Arguments = $"/c {path} --list"
            };

            var result = process.StartWaitWithRedirect();
            foreach (var s in result.StandardOutput.Split(Environment.NewLine))
            {
                if (s == "--list" || s == "__consumer_offsets" || s.Trim().Length == 0)
                {
                    continue;
                }

                yield return s.Trim();
            }
        }

        public RunProcessResult AlterPartitions(string topic, int partitions)
        {
            var path = BuildBaseKafkaCommand("kafka-topics");
            using var process = new ProcessJob
            {
                ExecutableName = "cmd.exe",
                Arguments = $"/c {path} --alter --topic {topic} --partitions {partitions}"
            };

            return process.StartWaitWithRedirect();
        }

        public RunProcessResult ExecutePartitionReassignment(string manifestFile, bool verify = false)
        {
            var path = BuildBaseKafkaCommand("kafka-reassign-partitions");
            var executionMode = verify ? "--verify" : "--execute";
            using var process = new ProcessJob
            {
                ExecutableName = "cmd.exe",
                Arguments = $"/c {path} --reassignment-json-file {manifestFile} {executionMode}"
            };

            return process.StartWaitWithRedirect();
        }

        public void WritePartitionReassignmentManifest(string outputPath, IList<string> topics)
        {
            var manifest = new KafkaReassignmentManifest
            {
                Version = 1,
                Partitions = topics.Select(topic => new KafkaPartition
                {
                    Topic = topic,
                    Partition = 0,
                    Replicas = new[] {1, 2}
                }).Concat(topics.Select(topic => new KafkaPartition
                {
                    Topic = topic,
                    Partition = 1,
                    Replicas = new[] {2, 3}
                })).OrderBy(partition => partition.Topic).ToList()
            };

            var serializer = new JsonSerializer();
            using var streamWriter = new StreamWriter(outputPath);
            using var jsonWriter = new JsonTextWriter(streamWriter);
            serializer.Serialize(jsonWriter, manifest);
        }

        private string BuildBaseKafkaCommand(string scriptName)
        {
            var path = Path.Combine(KafkaScriptsDirectory, scriptName);
            var commandConfig = !string.IsNullOrEmpty(CommandConfigFile) ? $" --command-config {CommandConfigFile}" : "";
            var servers = BootstrapServers.Aggregate("", (sum, current)=> $"{sum}{current.Host}:{current.Port},")[..^1];

            return $"{path} --bootstrap-server {servers}{commandConfig}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MigrasiLogee.Helpers;
using MigrasiLogee.Infrastructure;
using MigrasiLogee.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MigrasiLogee.Pipelines
{
    public class ScalePodsSettings : CommandSettings
    {
        [CommandArgument(0, "<PROJECT_NAME>")]
        [Description("Namespace/project name containing deployments to scale")]
        public string ProjectName { get; set; }

        [CommandOption("-m|--mode <MODE>")]
        [DefaultValue("oc")]
        [Description("Kubernetes server type, specify 'oc' or 'k3s', defaults to 'oc'")]
        public string Mode { get; set; }
        
        [CommandOption("-r|--replicas <REPLICAS>")]
        [DefaultValue(0)]
        [Description("Number of pods/replica per deployment, defaults to 0")]
        public int Replicas { get; set; }

        [CommandOption("-p|--prefix <PREFIX>")]
        [Description("Only scale deployment with this prefix. If no prefix is specified, then it will scale all deployment in the project")]
        public string Prefix { get; set; }

        [CommandOption("-f|--kubeconfig <KUBECONFIG_FILE>")]
        [Description("Kubeconfig .yml file to specify K3s login and context, if you're using 'k3s' mode")]
        public string KubeconfigFile { get; set; }

        [CommandOption("--oc <OC_PATH>")]
        [Description("Relative/full path to 'oc' executable (or leave empty if it's in PATH)")]
        public string OcPath { get; set; }

        [CommandOption("--kubectl <KUBECTL_PATH>")]
        [Description("Relative/full path to 'kubectl' executable (or leave empty if it's in PATH)")]
        public string KubectlPath { get; set; }
    }

    public class ScalePodsPipeline : PipelineBase<ScalePodsSettings>
    {
        private readonly KubectlClient _kubectl = new();
        private readonly OpenShiftClient _oc = new();

        protected override bool ValidateState(CommandContext context, ScalePodsSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ProjectName))
            {
                MessageWriter.ArgumentNotSpecifiedMessage("<PROJECT_NAME>");
                return false;
            }

            _oc.ProjectName = settings.ProjectName;
            _kubectl.NamespaceName = settings.ProjectName;

            if (settings.Replicas < 0)
            {
                AnsiConsole.MarkupLine("[red]Replicas cannot be less than 0.[/]");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.Prefix))
            {
                AnsiConsole.MarkupLine("[yellow]No prefix is specified, all deployments will be scaled.[/]");
            }

            if (settings.Mode == "oc")
            {
                var ocPath = DependencyLocator.WhereExecutable(settings.OcPath, OpenShiftClient.OcExecutableName);
                if (ocPath == null)
                {
                    MessageWriter.ExecutableNotFoundMessage(OpenShiftClient.OcExecutableName, "--oc");
                    return false;
                }

                _oc.OcExecutable = ocPath;
            }
            else if (settings.Mode == "k3s")
            {
                var kubectlPath = DependencyLocator.WhereExecutable(settings.KubectlPath, KubectlClient.KubectlExecutableName);
                if (kubectlPath == null)
                {
                    MessageWriter.ExecutableNotFoundMessage(KubectlClient.KubectlExecutableName, "--kubectl");
                    return false;
                }

                _kubectl.KubectlExecutable = kubectlPath;

                if (!DependencyLocator.IsFileExists(settings.KubeconfigFile))
                {
                    AnsiConsole.MarkupLine("[red]Kubeconfig file not found! Specify the path using --kubeconfig option.[/]");
                    return false;
                }

                _kubectl.KubeconfigFilePath = settings.KubeconfigFile;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]The specified mode is invalid. Valid values are 'oc' and 'k3s'.[/]");
                return false;
            }

            return true;
        }

        protected override void PreRun(CommandContext context, ScalePodsSettings settings)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Render(new Text("{ Deployment Replica Scale Up/Down }").Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            AnsiConsole.WriteLine("Project : {0}", settings.ProjectName);
            AnsiConsole.WriteLine();
        }

        protected override int Run(CommandContext context, ScalePodsSettings settings)
        {
            AnsiConsole.WriteLine("Discovering deployments...");
            var deployments = InternalGetDeployments(settings)
                .Where(x => string.IsNullOrWhiteSpace(settings.Prefix) || x.Contains(settings.Prefix))
                .ToList();

            AnsiConsole.MarkupLine($"Found [yellow]{deployments.Count}[/] deployments.");

            var table = new Table().LeftAligned();
            AnsiConsole.Live(table)
                .Overflow(VerticalOverflow.Ellipsis)
                .Start(ctx =>
                {
                    table.AddColumn("Deployment");
                    table.AddColumn("Replicas");
                    table.AddColumn("Status");
                    ctx.Refresh();

                    foreach (var deployment in deployments)
                    {
                        string statusMarkup;
                        try
                        {
                            InternalScale(settings, deployment);
                            statusMarkup = "[green]OK[/]";
                        }
                        catch (Exception e)
                        {
                            statusMarkup = $"[red]{e.Message.TrimLength(20)}[/]";
                        }

                        table.AddRow(deployment, settings.Replicas.ToString(), statusMarkup);
                        ctx.Refresh();
                    }
                });

            return 0;
        }

        private IEnumerable<string> InternalGetDeployments(ScalePodsSettings settings)
        {
            return settings.Mode == "oc" ? _oc.GetDeploymentNames() : _kubectl.GetDeploymentNames();
        }

        private void InternalScale(ScalePodsSettings settings, string deploymentName)
        {
            if (settings.Mode == "oc")
            {
                _oc.Scale(deploymentName, settings.Replicas);
            }
            else
            {
                _kubectl.Scale(deploymentName, settings.Replicas);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console.Cli;

namespace MigrasiLogee.Pipelines
{
    public class DumpMongoDbSettings : CommandSettings
    {
        [CommandArgument(0, "<PROJECT_NAME>")]
        [Description("Project name containing deployments to scale")]
        public string ProjectName { get; set; }

        [CommandArgument(1, "<OUTPUT_PATH>")]
        [Description("Full path to otutput file (CSV)")]
        public string OutputPath { get; set; }

        [CommandOption("-p|--prefix <PREFIX>")]
        [Description("Only process deployment with this prefix. If no prefix is specified, then it will try all deployment in the project")]
        public string Prefix { get; set; }

        [CommandOption("--oc <OC_PATH>")]
        [Description("Relative/full path to 'oc' executable (or leave empty if it's in PATH)")]
        public string OcPath { get; set; }

        [CommandOption("--mongodump <MONGODUMP_PATH>")]
        [Description("Relative/full path to 'mongodump' executable (or leave empty if it's in PATH)")]
        public string MongoDumpPath { get; set; }
    }

    public class DumpMongoDbPipeline : PipelineBase<DumpMongoDbSettings>
    {
        protected override bool ValidateState(CommandContext context, DumpMongoDbSettings settings)
        {
            throw new NotImplementedException();
        }

        protected override int Run(CommandContext context, DumpMongoDbSettings settings)
        {
            throw new NotImplementedException();
        }
    }
}

using Spectre.Console.Cli;

namespace MigrasiLogee.Pipelines
{
    public abstract class PipelineBase<T> : Command<T> where T : CommandSettings
    {
        public override int Execute(CommandContext context, T settings)
        {
            if (!ValidateState(context, settings))
            {
                return -1;
            }

            PreRun(context, settings);
            return Run(context, settings);
        }

        protected abstract bool ValidateState(CommandContext context, T settings);
        protected abstract int Run(CommandContext context, T settings);

        protected virtual void PreRun(CommandContext context, T settings)
        {
            // do nothing
        }
    }
}

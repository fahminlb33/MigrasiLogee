#nullable enable

using System.IO;
using MigrasiLogee.Helpers;

namespace MigrasiLogee.Infrastructure
{
    public static class DependencyLocator
    {
        public static bool IsFileExists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public static string? WhereCurl(string suppliedPath)
        {
            if (File.Exists(suppliedPath))
            {
                return suppliedPath;
            }

            var pathEnv = PathHelpers.GetFullPathToEnv("curl.exe");
            return pathEnv;
        }

        public static bool IsCurlSupported(string curlPath)
        {
            using var job = new ProcessJob
            {
                ExecutableName = curlPath,
                Arguments = "--dns-servers 8.8.8.8 google.com"
            };

            var result = job.StartWaitWithRedirect();
            return result.ExitCode == 0;
        }

        public static string? WhereDig(string suppliedPath)
        {
            if (File.Exists(suppliedPath))
            {
                return suppliedPath;
            }

            var pathEnv = PathHelpers.GetFullPathToEnv("dig.exe");
            return pathEnv;
        }
    }
}

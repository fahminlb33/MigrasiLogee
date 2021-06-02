#nullable enable

using System;
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

            var pathEnv = PathHelpers.GetFullPathToEnv(GetExtensionSuffix("curl"));
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

            var pathEnv = PathHelpers.GetFullPathToEnv(GetExtensionSuffix("dig"));
            return pathEnv;
        }

        private static string GetExtensionSuffix(string name)
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ? name + ".exe" : name;
        }
    }
}

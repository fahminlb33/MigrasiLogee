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

        public static string? WhereExecutable(string suppliedPath, string executableName)
        {
            if (File.Exists(suppliedPath))
            {
                return suppliedPath;
            }

            executableName = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? executableName + ".exe"
                : executableName;
            return PathHelpers.GetFullPathToEnv(executableName);
        }
    }
}

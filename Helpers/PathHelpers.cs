using System;
using System.IO;
using System.Linq;

namespace MigrasiLogee.Helpers
{
    public static class PathHelpers
    {
        public static string GetFullPathToEnv(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                {
                    return Path.GetFullPath(fileName);
                }

                var values = Environment.GetEnvironmentVariable("PATH");
                return values?.Split(Path.PathSeparator).Select(path => Path.Combine(path, fileName)).FirstOrDefault(File.Exists);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

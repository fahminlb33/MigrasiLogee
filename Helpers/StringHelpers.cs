using System;

namespace MigrasiLogee.Helpers
{
    public static class StringHelpers
    {
        public static readonly string[] NewlineCharacters = new[] {"\r\n", "\r", "\n"};

        public static string TrimLength(this string s, int maxLength = 30)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }

            return s.Length <= maxLength ? s : s.Substring(0, maxLength) + "...";
        }

        public static string TrimLengthFlatten(this string s, int maxLength = 30)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }

            s = s.Replace(Environment.NewLine, "");
            return s.Length <= maxLength ? s : s.Substring(0, maxLength) + "...";
        }

        public static int ParseInt(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? 0 : int.Parse(s);
        }

        public static string NormalizeKubeResourceName(string podName)
        {
            return podName.Split('/')[1];
        }
    }
}

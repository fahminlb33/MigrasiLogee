namespace MigrasiLogee.Helpers
{
    public static class StringHelpers
    {
        public static string TrimLength(this string s, int maxLength = 30)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }

            return s.Length <= maxLength ? s : s.Substring(0, maxLength) + "...";
        }
    }
}

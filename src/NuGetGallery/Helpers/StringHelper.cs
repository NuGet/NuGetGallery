using System;
using System.Globalization;

namespace NuGetGallery.Helpers
{
    public static class StringHelper
    {
        public static string[] SplitSafe(this string s, char[] separator, StringSplitOptions stringSplitOptions)
        {
            if (s == null)
            {
                return new string[0];
            }

            return s.Split(separator, stringSplitOptions);
        }

        public static string TruncateAtWordBoundary(this string input, int length = 300, string ommission = "...", string morText = "")
        {
            if (string.IsNullOrEmpty(input) || input.Length < length)
                return input;

            int nextSpace = input.LastIndexOf(" ", length, StringComparison.Ordinal);

            return string.Format(CultureInfo.CurrentCulture, "{2}{1}{0}",
                                 morText,
                                 ommission,
                                 input.Substring(0, (nextSpace > 0) ? nextSpace : length).Trim());
        }
    }
}
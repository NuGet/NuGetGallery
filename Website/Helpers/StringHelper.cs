using System;

namespace NuGetGallery.Helpers
{
    internal static class StringHelper
    {
        public static string[] SplitSafe(this string s, char[] separator, StringSplitOptions stringSplitOptions)
        {
            if (s == null)
            {
                return new string[0];
            }

            return s.Split(separator, stringSplitOptions);
        }
    }
}
using System;

namespace NuGetGallery.Helpers
{
    public static class StringExtensions
    {
        public static string[] SplitSafe(this string s, char[] separator, StringSplitOptions stringSplitOptions)
        {
            if (s == null)
            {
                return new string[0];
            }

            return s.Split(separator, stringSplitOptions);
        }

        public static string Abbreviate(this string text, int length)
        {
            if (text.Length <= length)
            {
                return text;
            }

            char[] delimiters = { ' ', '.', ',', ':', ';' };
            int index = text.LastIndexOfAny(delimiters, length - 3);

            if (index > (length / 2))
            {
                return text.Substring(0, index) + "...";
            }

            return text.Substring(0, length - 3) + "...";
        }


    }
}
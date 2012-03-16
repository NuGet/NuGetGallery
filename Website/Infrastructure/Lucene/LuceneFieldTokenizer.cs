using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public static class LuceneIdTokenizer
    {
        private static readonly char[] idSeparators = new[] { '.', '-', ' ' };

        public static IList<string> Tokenize(string term)
        {
            return TokenizeCamelCase(term).SelectMany(s => s.Split(idSeparators, StringSplitOptions.RemoveEmptyEntries)).ToList();
        }

        internal static IEnumerable<string> TokenizeCamelCase(string term)
        {
            if (term.Length < 2)
            {
                yield break;
            }

            int tokenStart = 0;
            for (int i = 1; i < term.Length; i++)
            {
                if (Char.IsUpper(term[i]) && (i - tokenStart > 2))
                {
                    yield return term.Substring(tokenStart, i - tokenStart);
                    tokenStart = i;
                }
            }
            if (term.Length - tokenStart < 2)
            {
                yield break;
            }
            yield return term.Substring(tokenStart);
        }
    }
}
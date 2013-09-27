using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class TokenizingHelper
    {
        public static IEnumerable<string> CamelCaseSplit(string term)
        {
            if (term.Length == 0)
            {
                yield break;
            }

            if (term.Length == 1)
            {
                yield return term;
                yield break;
            }

            string word = term[0].ToString();
            bool lastIsUpper = Char.IsUpper(term[0]);

            for (int i = 1; i < term.Length; i++)
            {
                bool currentIsUpper = Char.IsUpper(term[i]);

                if (lastIsUpper == false && currentIsUpper == true)
                {
                    yield return word;
                    word = string.Empty;
                }

                word += term[i];

                lastIsUpper = currentIsUpper;
            }

            yield return word;
            yield break;
        }
    }
}

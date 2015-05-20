// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public static class TokenizingHelper
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
            bool lastIsLetter = Char.IsLetter(term[0]);

            for (int i = 1; i < term.Length; i++)
            {
                bool currentIsUpper = Char.IsUpper(term[i]);
                bool currentIsLetter = Char.IsLetter(term[i]);

                if ((lastIsLetter && currentIsLetter) && (!lastIsUpper && currentIsUpper))
                {
                    yield return word;
                    word = string.Empty;
                }

                word += term[i];

                lastIsUpper = currentIsUpper;
                lastIsLetter = currentIsLetter;
            }

            yield return word;
            yield break;
        }

        private static ISet<string> _stopWords = new HashSet<string> 
        {
            "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "i",
            "if", "in", "into", "is", "it", "its", "no", "not", "of", "on", "or", "s", "such",
            "that", "the", "their", "then", "there", "these", "they", "this", "to", 
            "was", "we", "will", "with"
        };

        public static ISet<string> GetStopWords()
        {
            return _stopWords;
        }
    }
}

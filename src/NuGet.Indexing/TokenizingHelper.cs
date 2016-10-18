// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Indexing
{
    public static class TokenizingHelper
    {
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

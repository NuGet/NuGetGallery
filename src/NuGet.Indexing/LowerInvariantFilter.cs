// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;

namespace NuGet.Indexing
{
    /// <summary>
    /// Uses <see cref="char.ToLowerInvariant(char)"/> instead of <see cref="char.ToLower(char)"/>. Based off of
    /// <see cref="https://github.com/apache/lucenenet/blob/19c02b21064f1232132c46f8eb22db7ee3f819f7/src/core/Analysis/LowerCaseFilter.cs"/>.
    /// </summary>
    public class LowerInvariantFilter : TokenFilter
    {
        private readonly ITermAttribute _termAttribute;

        public LowerInvariantFilter(TokenStream input) : base(input)
        {
            _termAttribute = AddAttribute<ITermAttribute>();
        }

        public override bool IncrementToken()
        {
            if (input.IncrementToken())
            {
                var buffer = _termAttribute.TermBuffer();
                var length = _termAttribute.TermLength();
                for (int i = 0; i < length; i++)
                {
                    buffer[i] = char.ToLowerInvariant(buffer[i]);
                }

                return true;
            }

            return false;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;

namespace NuGet.IndexingTests.TestSupport
{
    public static class TokenStreamExtensions
    {
        public static IEnumerable<TokenAttributes> Tokenize(this TokenStream tokenStream)
        {
            var term = tokenStream.GetAttribute<ITermAttribute>();
            var offset = tokenStream.GetAttribute<IOffsetAttribute>();

            IPositionIncrementAttribute positionIncrement = null;

            if (tokenStream.HasAttribute<IPositionIncrementAttribute>())
            {
                positionIncrement = tokenStream.GetAttribute<IPositionIncrementAttribute>();
            }

            while (tokenStream.IncrementToken())
            {
                var tokenAttributes = new TokenAttributes(term.Term, offset.StartOffset, offset.EndOffset);

                if (positionIncrement != null)
                {
                    tokenAttributes.PositionIncrement = positionIncrement.PositionIncrement;
                }

                yield return tokenAttributes;
            }
        }

        public static TokenAttributes[] Tokenize(this Analyzer analyzer, string text)
        {
            var tokenStream = analyzer.TokenStream(null, new StringReader(text));
            return tokenStream.Tokenize().ToArray();
        }
    }
}

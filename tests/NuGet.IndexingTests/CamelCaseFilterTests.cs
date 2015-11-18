// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Tokenattributes;
using NuGet.Indexing;
using Xunit;
using Lucene.Net.Util;
using NuGet.IndexingTests.TestSupport;

namespace NuGet.IndexingTests
{
    public class CamelCaseFilterTests
    {
        [Theory, MemberData("TheoryData")]
        public void Theory(string text, TokenAttributes[] expected)
        {
            // ARRANGE, ACT
            var actual = this.GetTokenAttributes(text).ToArray();

            // ASSERT
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].Term, actual[i].Term);
                Assert.Equal(expected[i].StartOffset, actual[i].StartOffset);
                Assert.Equal(expected[i].EndOffset, actual[i].EndOffset);
                Assert.Equal(expected[i].PositionIncrement, actual[i].PositionIncrement);
            }
        }

        private IEnumerable<TokenAttributes> GetTokenAttributes(string text)
        {
            var textReader = new StringReader(text);
            var tokenStream = new StandardTokenizer(Version.LUCENE_30, textReader);
            var filter = new CamelCaseFilter(tokenStream);

            var termAttribute = filter.GetAttribute<ITermAttribute>();
            var offsetAttribute = filter.GetAttribute<IOffsetAttribute>();
            var positionIncrementAttribute = filter.GetAttribute<IPositionIncrementAttribute>();

            while (filter.IncrementToken())
            {
                yield return new TokenAttributes
                {
                    Term = termAttribute.Term,
                    StartOffset = offsetAttribute.StartOffset,
                    EndOffset = offsetAttribute.EndOffset,
                    PositionIncrement = positionIncrementAttribute.PositionIncrement
                };
            }
        }

        public static IEnumerable<object[]> TheoryData
        {
            get
            {
                yield return new object[]
                {
                    "AaBbCcDd",
                    new[]
                    {
                        new TokenAttributes("AaBbCcDd", 0, 8, 1),
                        new TokenAttributes("Aa", 0, 2, 0),
                        new TokenAttributes("AaBb", 0, 4, 0),
                        new TokenAttributes("Bb", 2, 4, 1),
                        new TokenAttributes("BbCc", 2, 6, 0),
                        new TokenAttributes("Cc", 4, 6, 1),
                        new TokenAttributes("CcDd", 4, 8, 0),
                        new TokenAttributes("Dd", 6, 8, 1)
                    }
                };

                yield return new object[]
                {
                    "AaBbCc",
                    new[]
                    {
                        new TokenAttributes("AaBbCc", 0, 6, 1),
                        new TokenAttributes("Aa", 0, 2, 0),
                        new TokenAttributes("AaBb", 0, 4, 0),
                        new TokenAttributes("Bb", 2, 4, 1),
                        new TokenAttributes("BbCc", 2, 6, 0),
                        new TokenAttributes("Cc", 4, 6, 1)
                    }
                };

                yield return new object[]
                {
                    "AaBb",
                    new[]
                    {
                        new TokenAttributes("AaBb", 0, 4, 1),
                        new TokenAttributes("Aa", 0, 2, 0),
                        new TokenAttributes("Bb", 2, 4, 1)
                    }
                };

                yield return new object[]
                {
                    "Aa",
                    new[]
                    {
                        new TokenAttributes("Aa", 0, 2, 1)
                    }
                };

                yield return new object[]
                {
                    string.Empty,
                    new object[0]
                };

                yield return new object[]
                {
                    "ABCD",
                    new[]
                    {
                        new TokenAttributes("ABCD", 0, 4, 1)
                    }
                };

                yield return new object[]
                {
                    "ABCde",
                    new[]
                    {
                        new TokenAttributes("ABCde", 0, 5, 1)
                    }
                };

                yield return new object[]
                {
                    "AaB",
                    new[]
                    {
                        new TokenAttributes("AaB", 0, 3, 1),
                        new TokenAttributes("Aa", 0, 2, 0),
                        new TokenAttributes("B", 2, 3, 1)
                    }
                };

                yield return new object[]
                {
                    "Abcd",
                    new[]
                    {
                        new TokenAttributes("Abcd", 0, 4, 1)
                    }
                };

                yield return new object[]
                {
                    "A1a2B3b",
                    new[]
                    {
                        new TokenAttributes("A1a2B3b", 0, 7, 1)
                    }
                };
            }
        }
    }
}

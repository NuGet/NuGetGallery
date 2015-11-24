// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class TagsAnalyzerTests
    {
        [Theory]
        [MemberData(nameof(TokenizerLowercasesAndSplitsInputData))]
        public void TokenizerLowercasesAndSplitsInput(string text, TokenAttributes[] expected)
        {
            // arrange, act
            var actual = new TagsAnalyzer().Tokenize(text);

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> TokenizerLowercasesAndSplitsInputData
        {
            get
            {
                // split by DotTokenizer
                yield return new object[]
                {
                    "Split sentence.",
                    new[]
                    {
                        new TokenAttributes("split", 0, 5),
                        new TokenAttributes("sentence", 6, 14)
                    }
                };

                // lower case 
                yield return new object[]
                {
                    "D",
                    new[]
                    {
                        new TokenAttributes("d", 0, 1)
                    }
                };

                // leaves stop words
                yield return new object[]
                {
                    "This is a sentence full of stop words.",
                    new[]
                    {
                        new TokenAttributes("this", 0, 4),
                        new TokenAttributes("is", 5, 7),
                        new TokenAttributes("a", 8, 9),
                        new TokenAttributes("sentence", 10, 18),
                        new TokenAttributes("full", 19, 23),
                        new TokenAttributes("of", 24, 26),
                        new TokenAttributes("stop", 27, 31),
                        new TokenAttributes("words", 32, 37)
                    }
                };
            }
        }
    }
}

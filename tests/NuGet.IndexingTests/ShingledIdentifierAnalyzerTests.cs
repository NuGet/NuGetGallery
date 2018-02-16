// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class ShingledIdentifierAnalyzerTests
    {
        [Theory]
        [MemberData(nameof(TokenizerShinglesAndLowercasesInputData))]
        public void TokenizerShinglesAndLowercasesInput(string text, TokenAttributes[] expected)
        {
            // arrange, act
            var actual = new ShingledIdentifierAnalyzer().Tokenize(text);

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> TokenizerShinglesAndLowercasesInputData
        {
            get
            {
                // split by DotTokenizer
                yield return new object[]
                {
                    "a.b",
                    new[]
                    {
                        new TokenAttributes("a", 0, 1, 1),
                        new TokenAttributes("a b", 0, 3, 0),
                        new TokenAttributes("b", 2, 3, 1)
                    }
                };

                // lower case 
                yield return new object[]
                {
                    "D",
                    new[]
                    {
                        new TokenAttributes("d", 0, 1, 1)
                    }
                };

                // consecutive seperators
                yield return new object[]
                {
                    "a.....b",
                    new[]
                    {
                        new TokenAttributes("a", 0, 1, 1),
                        new TokenAttributes("a b", 0, 7, 0),
                        new TokenAttributes("b", 6, 7, 1)
                    }
                };

                // shingle up to two
                yield return new object[]
                {
                    "a.b.c.d",
                    new[]
                    {
                        new TokenAttributes("a", 0, 1, 1),
                        new TokenAttributes("a b", 0, 3, 0),
                        new TokenAttributes("b", 2, 3, 1),
                        new TokenAttributes("b c", 2, 5, 0),
                        new TokenAttributes("c", 4, 5, 1),
                        new TokenAttributes("c d", 4, 7, 0),
                        new TokenAttributes("d", 6, 7, 1),
                    }
                };
            }
        }
    }
}

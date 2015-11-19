// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class IdentifierAutocompleteAnalyzerTests
    {
        [Theory]
        [MemberData(nameof(TokenizerLowercasesNGramsAndCamelCasesInputData))]
        public void TokenizerLowercasesNGramsAndCamelCasesInput(string text, TokenAttributes[] expected)
        {
            // arrange, act
            var actual = new IdentifierAutocompleteAnalyzer().Tokenize(text);

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> TokenizerLowercasesNGramsAndCamelCasesInputData
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
                        new TokenAttributes("b", 2, 3, 1)
                    }
                };

                // split on camel case
                yield return new object[]
                {
                    "DotNet",
                    new[]
                    {
                        new TokenAttributes("d", 0, 1, 1),
                        new TokenAttributes("do", 0, 2, 1),
                        new TokenAttributes("dot", 0, 3, 1),
                        new TokenAttributes("dotn", 0, 4, 1),
                        new TokenAttributes("dotne", 0, 5, 1),
                        new TokenAttributes("dotnet", 0, 6, 1),
                        new TokenAttributes("d", 0, 1, 1),
                        new TokenAttributes("do", 0, 2, 1),
                        new TokenAttributes("dot", 0, 3, 1),
                        new TokenAttributes("n", 3, 4, 1),
                        new TokenAttributes("ne", 3, 5, 1),
                        new TokenAttributes("net", 3, 6, 1)
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

                // ngram size one to eight
                yield return new object[]
                {
                    "DOTNETZIP NET",
                    new[]
                    {
                        new TokenAttributes("d", 0, 1, 1),
                        new TokenAttributes("do", 0, 2, 1),
                        new TokenAttributes("dot", 0, 3, 1),
                        new TokenAttributes("dotn", 0, 4, 1),
                        new TokenAttributes("dotne", 0, 5, 1),
                        new TokenAttributes("dotnet", 0, 6, 1),
                        new TokenAttributes("dotnetz", 0, 7, 1),
                        new TokenAttributes("dotnetzi", 0, 8, 1),
                        new TokenAttributes("n", 10, 11, 1),
                        new TokenAttributes("ne", 10, 12, 1),
                        new TokenAttributes("net", 10, 13, 1)
                    }
                };
            }

        }
    }
}

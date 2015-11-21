// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class IdentifierAnalyzerTests
    {
        [Theory]
        [MemberData("TokenizerLowercasesAndCamelCasesInputData")]
        public void TokenizerLowercasesAndCamelCasesInput(string text, TokenAttributes[] expected)
        {
            // arrange, act
            var actual = new IdentifierAnalyzer().Tokenize(text);

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> TokenizerLowercasesAndCamelCasesInputData
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
                        new TokenAttributes("dotnet", 0, 6, 1),
                        new TokenAttributes("dot", 0, 3, 0),
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
            }
        }
    }
}

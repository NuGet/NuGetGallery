// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class IdentifierKeywordAnalyzerTests
    {
        [Theory]
        [MemberData(nameof(TokenizerOnlyLowercasesInputData))]
        public void TokenizerOnlyLowercasesInput(string text, TokenAttributes expected)
        {
            // arrange, act
            var actual = new IdentifierKeywordAnalyzer().Tokenize(text);

            // assert
            Assert.Equal(new[] { expected }, actual);
        }

        public static IEnumerable<object[]> TokenizerOnlyLowercasesInputData
        {
            get
            {
                // all uppercase
                yield return new object[] { "DOTNET", new TokenAttributes("dotnet", 0, 6) };

                // camel case
                yield return new object[] { "DotNet", new TokenAttributes("dotnet", 0, 6) };

                // lower case
                yield return new object[] { "dotnet", new TokenAttributes("dotnet", 0, 6) };

                // stop words and spaces
                yield return new object[] { "A BAD identifier.", new TokenAttributes("a bad identifier.", 0, 17) };

                // mixed
                yield return new object[] { "DotNet.ZIP-Unofficial is a BAD identifier.", new TokenAttributes("dotnet.zip-unofficial is a bad identifier.", 0, 42) };
            }

        }
    }
}

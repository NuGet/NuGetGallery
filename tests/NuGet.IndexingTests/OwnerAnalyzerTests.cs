// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class OwnerAnalyzerTests
    {
        [Theory]
        [MemberData(nameof(TokenizerOnlyLowercasesOwnerInputData))]
        public void TokenizerOnlyLowercasesOwnerInput(string text, TokenAttributes expected)
        {
            // arrange, act
            var actual = new OwnerAnalyzer().Tokenize(text);

            // assert
            Assert.Equal(new[] { expected }, actual);
        }

        public static IEnumerable<object[]> TokenizerOnlyLowercasesOwnerInputData
        {
            get
            {
                // all upper case
                yield return new object[] { "MICROSOFT-OWNER", new TokenAttributes("microsoft-owner", 0, 15) };

                // title case
                yield return new object[] { "Microsoft.Owner", new TokenAttributes("microsoft.owner", 0, 15) };

                // camel case
                yield return new object[] { "MicrosoftOwner", new TokenAttributes("microsoftowner", 0, 14) };

                // mixed
                yield return new object[] { "a Microsoft OWNER.", new TokenAttributes("a microsoft owner.", 0, 18) };
            }

        }
    }
}

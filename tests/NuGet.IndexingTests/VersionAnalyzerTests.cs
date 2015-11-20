// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class VersionAnalyzerTests
    {
        [Theory]
        [MemberData("TokenizerNormalizesVersionInputData")]
        public void TokenizerNormalizesVersionInput(string text, TokenAttributes expected)
        {
            // arrange, act
            var actual = new VersionAnalyzer().Tokenize(text);

            // assert
            Assert.Equal(new[] { expected }, actual);
        }

        public static IEnumerable<object[]> TokenizerNormalizesVersionInputData
        {
            get
            {
                // one dot
                yield return new object[] { "1.0", new TokenAttributes("1.0.0", 0, 3) };

                // extra zeros
                yield return new object[] { "2.003.1", new TokenAttributes("2.3.1", 0, 7) };

                // empty
                yield return new object[] { string.Empty, new TokenAttributes(string.Empty, 0, 0) };

                // lots of digits
                yield return new object[] { "1.02.3456789", new TokenAttributes("1.2.3456789", 0, 12) };

                // trim
                yield return new object[] { " 1.02.03    ", new TokenAttributes("1.2.3", 0, 12) };
            }

        }
    }
}

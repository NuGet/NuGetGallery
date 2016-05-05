// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class ExpandAcronymsFilterTests
    {
        [Theory]
        [MemberData(nameof(TokenizingReturnsExpectedTermsData))]
        public void TokenizingReturnsExpectedTerms(string text, TokenAttributes[] expected)
        {
            // Arrange
            var tokenStream = new StandardTokenizer(Version.LUCENE_30, new StringReader(text));
            var filter = new ExpandAcronymsFilter(tokenStream, NuGetAcronymExpansionProvider.Instance);

            // Act
            var actual = filter.Tokenize().ToArray();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("foobar", "foo", "bar")]
        [InlineData("foobarfoo", "foo", "bar")]
        [InlineData("foobarfoobar", "foo", "barbar")]
        [InlineData("fOObar", "foo", "bar")]
        [InlineData("fOObarFOO", "foo", "bar")]
        [InlineData("FOObarfoobar", "foo", "barbar")]
        [InlineData("fooBAR", "foo", "BAR")]
        [InlineData("fooBAR", "FOO", "BAR")]
        [InlineData("FOOBAR", "foo", "BAR")]
        public void RemoveSubstringRemovesSubstringFromString(string original, string substringToRemove, string expected)
        {
            // Act
            var result = ExpandAcronymsFilter.RemoveSubstring(original, substringToRemove);

            // Assert
            Assert.Equal(expected, result);
        }

        public static IEnumerable<object[]> TokenizingReturnsExpectedTermsData
        {
            get
            {
                yield return new object[]
                {
                    "xamlbehaviors",
                    new[]
                    {
                        new TokenAttributes("xamlbehaviors", 0, 13, 1),
                        new TokenAttributes("xaml", 0, 13, 0),
                        new TokenAttributes("behaviors", 0, 13, 0)
                    }
                };

                yield return new object[]
                {
                    "uwpef",
                    new[]
                    {
                        new TokenAttributes("uwpef", 0, 5, 1),
                        new TokenAttributes("ef", 0, 5, 0),
                        new TokenAttributes("entity framework", 0, 5, 0),
                        new TokenAttributes("uwp", 0, 5, 0),
                        new TokenAttributes("universal windows platform", 0, 5, 0)
                    }
                };
            }
        }
    }
}
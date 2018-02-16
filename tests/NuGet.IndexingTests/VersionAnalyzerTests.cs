// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using NuGet.Versioning;
using Xunit;

namespace NuGet.IndexingTests
{
    public class VersionAnalyzerTests
    {
        [Theory]
        [MemberData(nameof(CaseSensitiveTokenizerNormalizesVersionInputData))]
        public void CaseSensitiveTokenizerNormalizesVersionInput(string text, TokenAttributes expected)
        {
            // Arrange
            var analyzer = new VersionAnalyzer(caseSensitive: true);

            // Act
            var actual = analyzer.Tokenize(text);

            // Assert
            Assert.Equal(new[] { expected }, actual);
        }

        [Theory]
        [MemberData(nameof(CaseInsensitiveTokenizerNormalizesVersionInputData))]
        public void CaseInsensitiveTokenizerNormalizesVersionInput(string text, TokenAttributes expected)
        {
            // Arrange
            var analyzer = new VersionAnalyzer(caseSensitive: false);

            // Act
            var actual = analyzer.Tokenize(text);

            // Assert
            Assert.Equal(new[] { expected }, actual);
        }

        public static IEnumerable<object[]> CaseSensitiveTokenizerNormalizesVersionInputData => TestCases
            .Select(x => new object[] { x.Input, x.CaseSensitive });

        public static IEnumerable<object[]> CaseInsensitiveTokenizerNormalizesVersionInputData => TestCases
            .Select(x => new object[] { x.Input, x.CaseInsensitive });

        private static IEnumerable<TestCase> TestCases
        {
            get
            {
                // one dot
                yield return new TestCase("1.0");

                // extra zeros
                yield return new TestCase("2.003.1");

                // empty
                yield return new TestCase(string.Empty);

                // lots of digits
                yield return new TestCase("1.02.3456789");

                // trim
                yield return new TestCase(" 1.02.03    ");

                // uppercase
                yield return new TestCase("1.0.0-ALPHA");

                // lowercase
                yield return new TestCase("1.0.0-alpha");

                // mixed case
                yield return new TestCase("1.0.0-AlPhA");

                // build metadata
                yield return new TestCase("1.0.0+GIT");

                // dots in prerelease
                yield return new TestCase("1.0.0-alpha.1");
            }
        }

        private class TestCase
        {
            public TestCase(string input)
            {
                string normalized;
                if (NuGetVersion.TryParse(input, out NuGetVersion version))
                {
                    normalized = version.ToNormalizedString();
                }
                else
                {
                    normalized = input;
                }
                
                Input = input;
                CaseSensitive = new TokenAttributes(normalized, 0, input.Length);
                CaseInsensitive = new TokenAttributes(normalized.ToLowerInvariant(), 0, input.Length);
            }

            public string Input { get; }
            public TokenAttributes CaseSensitive { get; }
            public TokenAttributes CaseInsensitive { get; }
        }
    }
}

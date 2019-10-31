// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using GitHubVulnerabilities2Db.Ingest;
using Xunit;

namespace GitHubVulnerabilities2Db.Facts
{
    public class GitHubVersionRangeParserFacts
    {
        private readonly GitHubVersionRangeParser Parser = new GitHubVersionRangeParser();

        [Theory]
        [InlineData((string)null)] // cannot be null
        [InlineData("")] // cannot be empty
        [InlineData("      ")] // cannot be whitespace
        [InlineData("a a b b c c")] // cannot have more than 3 parts
        [InlineData("asdfasdf")] // must have 2 * n number of parts
        [InlineData("<> 1.0.0")] // invalid symbol
        [InlineData("> 1.0.0, > 2.0.0")] // min already specified
        [InlineData("= 1.0.0, > 2.0.0")] // min already specified
        [InlineData("> 1.0.0, = 2.0.0")] // min already specified
        [InlineData("< 1.0.0, < 2.0.0")] // max already specified
        [InlineData("= 1.0.0, < 2.0.0")] // max already specified
        [InlineData("< 1.0.0, = 2.0.0")] // max already specified
        [InlineData("= 1.0.0, = 2.0.0")] // min and max already specified
        [InlineData("= 1.0.0, = 1.0.0")] // min and max already specified
        public void ThrowsGitHubVersionRangeParsingExceptionWithInvalidInput(string input)
        {
            var exception = Assert.Throws<GitHubVersionRangeParsingException>(() => Parser.ToNuGetVersionRange(input));
            Assert.Equal(input, exception.InvalidVersionRange);
        }

        [Theory]
        [InlineData("= 0.2.0", "[0.2.0, 0.2.0]")]
        [InlineData("<= 1.0.8", "(, 1.0.8]")]
        [InlineData("< 0.1.11", "(, 0.1.11)")]
        [InlineData(">= 1.0.8", "[1.0.8, )")]
        [InlineData("> 0.1.11", "(0.1.11, )")]
        [InlineData(">= 4.3.0, < 4.3.5", "[4.3.0, 4.3.5)")]
        [InlineData(">= 4.3.0, <= 4.3.5", "[4.3.0, 4.3.5]")]
        [InlineData("< 4.3.5, >= 4.3.0", "[4.3.0, 4.3.5)")]
        [InlineData("<= 4.3.5, >= 4.3.0", "[4.3.0, 4.3.5]")]
        [InlineData("> 4.3.0, < 4.3.5", "(4.3.0, 4.3.5)")]
        public void ReturnsExpectedRange(string input, string expected)
        {
            var actual = Parser.ToNuGetVersionRange(input);
            Assert.Equal(expected, actual.ToNormalizedString());
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Infrastructure
{
    public class NuGetQueryParserFacts
    {
        [Theory]
        [InlineData("hello", new[] { null, "hello" })]
        [InlineData("\"hello\"", new[] { null, "hello" })]
        [InlineData("Id:hello", new[] { "Id", "hello" })]
        [InlineData("Id:\"hello\"", new[] { "Id", "hello" })]
        [InlineData("\"Id:hello\"", new[] { null, "Id:hello" })]
        public void SingleResult(string input, string[] expectedResults)
        {
            var results = new NuGetQueryParser().Parse(input);
            Assert.Single(results);
            var expected = new NuGetSearchTerm
            {
                Field = expectedResults[0],
                TermOrPhrase = expectedResults[1]
            };
            Assert.Equal(expected, results[0]);
        }

        [Fact]
        public void MultipleResult()
        {
            var results = new NuGetQueryParser().Parse("\"hello you\" id:beautiful little:\"creatures\"");
            Assert.Equal(new NuGetSearchTerm { Field = null, TermOrPhrase = "hello you" }, results[0]);
            Assert.Equal(new NuGetSearchTerm { Field = "id", TermOrPhrase = "beautiful" }, results[1]);
            Assert.Equal(new NuGetSearchTerm { Field = "little", TermOrPhrase = "creatures" }, results[2]);
        }

        [Fact]
        public void EmptyString()
        {
            var results = new NuGetQueryParser().Parse("");
            Assert.Empty(results);
        }

        [Fact]
        public void SingleQuote()
        {
            var results = new NuGetQueryParser().Parse("\"");
            Assert.Single(results);
            Assert.Equal(new NuGetSearchTerm { TermOrPhrase = "" }, results[0]);
        }

        [Fact]
        public void EmptyPhrase()
        {
            var results = new NuGetQueryParser().Parse("\"\"");
            Assert.Single(results);
            Assert.Equal(new NuGetSearchTerm { TermOrPhrase = "" }, results[0]);
        }

        [Fact]
        public void LeadingColon()
        {
            var results = new NuGetQueryParser().Parse(":Foo");
            Assert.Single(results);
            Assert.Equal(new NuGetSearchTerm { TermOrPhrase = "Foo" }, results[0]);
        }

        [Fact]
        public void ExtraColon()
        {
            var results = new NuGetQueryParser().Parse("ID::Foo");
            Assert.Single(results);
            Assert.Equal(new NuGetSearchTerm { Field = "ID", TermOrPhrase = "Foo" }, results[0]);
        }

        [Fact]
        public void TermlessField()
        {
            var results = new NuGetQueryParser().Parse("Id:");
            Assert.Empty(results);
        }
    }
}

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
            Assert.Equal(expectedResults, results[0]);
        }

        [Fact]
        public void MultipleResult()
        {
            var results = new NuGetQueryParser().Parse("\"hello you\" id:beautiful little:\"creatures\"");
            Assert.Equal(new [] { null, "hello you" }, results[0]);
            Assert.Equal(new [] { "id", "beautiful" }, results[1]);
            Assert.Equal(new[] { "little", "creatures" }, results[2]);
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
            Assert.Equal(new[] { null, "" }, results[0]);
        }

        [Fact]
        public void EmptyPhrase()
        {
            var results = new NuGetQueryParser().Parse("\"\"");
            Assert.Single(results);
            Assert.Equal(new[] { null, "" }, results[0]);
        }

        [Fact]
        public void LeadingColon()
        {
            var results = new NuGetQueryParser().Parse(":Foo");
            Assert.Single(results);
            Assert.Equal(new[] { null, "Foo" }, results[0]);
        }

        [Fact]
        public void ExtraColon()
        {
            var results = new NuGetQueryParser().Parse("ID::Foo");
            Assert.Single(results);
            Assert.Equal(new[] { "ID", "Foo" }, results[0]);
        }

        [Fact]
        public void TermlessField()
        {
            var results = new NuGetQueryParser().Parse("Id:");
            Assert.Empty(results);
        }
    }
}

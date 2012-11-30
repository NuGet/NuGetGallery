using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Infrastructure
{
    public class NuGetQueryParserFacts
    {
        [Theory]
        [InlineData("hello", new[] { null, "hello", null })]
        [InlineData("\"hello\"", new[] { null, null, "hello" })]
        [InlineData("Id:hello", new[] { "Id", "hello", null })]
        [InlineData("Id:\"hello\"", new[] { "Id", null, "hello" })]
        [InlineData("\"Id:hello\"", new[] { null, null, "Id:hello" })]
        public void SingleResult(string input, string[] expectedResults)
        {
            var results = new NuGetQueryParser().Parse(input).list;
            Assert.Single(results);
            Assert.Equal(results[0], expectedResults);
        }

        [Fact]
        public void MultipleResult()
        {
            var results = new NuGetQueryParser().Parse("\"hello you\" id:beautiful little:\"creatures\"").list;
            Assert.Equal(results[0], new [] { null, null, "hello you" });
            Assert.Equal(results[1], new [] { "id", "beautiful", null });
            Assert.Equal(results[2], new[] { "little", null, "creatures" });
        }

        [Fact]
        public void EmptyString()
        {
            var results = new NuGetQueryParser().Parse("").list;
            Assert.Empty(results);
        }

        [Fact]
        public void SingleQuote()
        {
            var results = new NuGetQueryParser().Parse("\"").list;
            Assert.Single(results);
            Assert.Equal(results[0], new[] { null, null, "" });
        }

        [Fact]
        public void EmptyPhrase()
        {
            var results = new NuGetQueryParser().Parse("\"\"").list;
            Assert.Single(results);
            Assert.Equal(results[0], new[] { null, null, "" });
        }

        [Fact]
        public void LeadingColon()
        {
            var results = new NuGetQueryParser().Parse(":Foo").list;
            Assert.Single(results);
            Assert.Equal(results[0], new[] { null, "Foo", null});
        }

        [Fact]
        public void ExtraColon()
        {
            var results = new NuGetQueryParser().Parse("ID::Foo").list;
            Assert.Single(results);
            Assert.Equal(results[0], new[] { "ID", "Foo", null });
        }

        [Fact]
        public void TermlessField()
        {
            var results = new NuGetQueryParser().Parse("Id:").list;
            Assert.Empty(results);
        }
    }
}

using Xunit;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail
{
    public class MarkdownEmailBuilderFacts
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        public void EscapeMarkdown_ReturnsNullOrEmpty_WhenInputIsNullOrEmpty(string input, string expected)
        {
            var result = MarkdownEmailBuilder.EscapeMarkdown(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Hello World", "Hello World")]
        [InlineData("NoSpecialChars123", "NoSpecialChars123")]
        public void EscapeMarkdown_ReturnsUnchanged_WhenNoSpecialChars(string input, string expected)
        {
            var result = MarkdownEmailBuilder.EscapeMarkdown(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EscapeMarkdown_EscapesAllSpecialMarkdownCharacters()
        {
            string input = "\\`*_{}[]<>()#+-.!|";
            string expected = "\\\\\\`\\*\\_\\{\\}\\[\\]\\<\\>\\(\\)\\#\\+\\-\\.\\!\\|";
            var result = MarkdownEmailBuilder.EscapeMarkdown(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EscapeMarkdown_EscapesRepeatedSpecialCharacters()
        {
            string input = "**bold**";
            string expected = "\\*\\*bold\\*\\*";
            var result = MarkdownEmailBuilder.EscapeMarkdown(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EscapeMarkdown_MixedContent()
        {
            string input = "Hello *World*! [Click](link)";
            string expected = "Hello \\*World\\*\\! \\[Click\\]\\(link\\)";
            var result = MarkdownEmailBuilder.EscapeMarkdown(input);
            Assert.Equal(expected, result);
        }
    }
}

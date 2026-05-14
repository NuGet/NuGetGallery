// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Ganss.Xss;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class MarkdownServiceFacts
    {

        public class GetReadMeHtmlMethod
        {
            private readonly MarkdownService _markdownService;
            private readonly Mock<IFeatureFlagService> _featureFlagService;
            private readonly IHtmlSanitizer _htmlSanitizer;

            public GetReadMeHtmlMethod()
            {
                _featureFlagService = new Mock<IFeatureFlagService>();
                _htmlSanitizer = new HtmlSanitizer();
                _markdownService = new MarkdownService(_featureFlagService.Object, _htmlSanitizer);
            }

            [Theory]
            [InlineData(-1)]
            public void ThrowsArgumentOutOfRangeExceptionForNegativeIncrementHeadersBy(int incrementHeadersBy)
            {
                var exception = Assert.Throws<ArgumentOutOfRangeException>(() => _markdownService.GetHtmlFromMarkdown("markdown file test", incrementHeadersBy));
                Assert.Equal(nameof(incrementHeadersBy), exception.ParamName);
                Assert.Contains("must be greater than or equal to 0", exception.Message);
            }

            [Fact]
            public void ThrowsArgumentNullExceptionForNullMarkdownString()
            {
                Assert.Throws<ArgumentNullException>(() => _markdownService.GetHtmlFromMarkdown(null, 0));
                Assert.Throws<ArgumentNullException>(() => _markdownService.GetHtmlFromMarkdown(null));
            }

            [Theory]
            [InlineData("<script>alert('test')</script>", "")]
            [InlineData("<img src=\"javascript:alert('test');\">", "<img>")]
            [InlineData("<a href=\"javascript:alert('test');\">xss</a>", "<p><a rel=\"noopener noreferrer nofollow\">xss</a></p>")]
            public void RemovesUnsafeHtml(string originalMd, string expectedHtml)
            {
                Assert.Equal(expectedHtml, _markdownService.GetHtmlFromMarkdown(originalMd).Content);
            }

            [Theory]
            [InlineData("# Heading", "<h2 id=\"heading\">Heading</h2>", 1)]
            [InlineData("# Heading", "<h1 id=\"heading\">Heading</h1>", 0)]
            [InlineData("# Heading", "<h6 id=\"heading\">Heading</h6>", 6)]
            [InlineData("# Heading", "<h6 id=\"heading\">Heading</h6>", 7)]
            [InlineData("# Heading", "<h6 id=\"heading\">Heading</h6>", 5)]
            public void DemotesMarkdownHeadings(string originalMd, string expectedHtml, int incrementHeadersBy)
            {
                Assert.Equal(expectedHtml, _markdownService.GetHtmlFromMarkdown(originalMd, incrementHeadersBy).Content);
            }

            [Theory]
            [InlineData("# Heading", "<h2 id=\"heading\">Heading</h2>")]
            [InlineData("<!-- foo --> <!-- foo \n bar --> baz", "<p>baz</p>")]
            [InlineData("\ufeff# Heading with BOM", "<h2 id=\"heading-with-bom\">Heading with BOM</h2>")]
            [InlineData("- List", "<ul>\n<li>List</li>\n</ul>")]
            [InlineData("This is a paragraph\nwithout a break inside", "<p>This is a paragraph\nwithout a break inside</p>")]
            [InlineData("soft line break line1  \nline2  \nline3  ", "<p>soft line break line1<br>\nline2<br>\nline3</p>")]
            [InlineData("hard line break line1\n\nline2\n\nline3", "<p>hard line break line1</p>\n<p>line2</p>\n<p>line3</p>")]
            [InlineData("[text](http://www.test.com)", "<p><a href=\"https://www.test.com/\" rel=\"noopener noreferrer nofollow\">text</a></p>")]
            [InlineData("[text](javascript:alert('hi'))", "<p><a href=\"\" rel=\"noopener noreferrer nofollow\">text</a></p>")]
            [InlineData("> <text>Blockquote</text>", "<blockquote class=\"blockquote\">\n<p></p>\n</blockquote>")]
            [InlineData("> > <text>Blockquote</text>", "<blockquote class=\"blockquote\">\n<blockquote class=\"blockquote\">\n<p></p>\n</blockquote>\n</blockquote>")]
            [InlineData("[text](http://www.asp.net)", "<p><a href=\"https://www.asp.net/\" rel=\"noopener noreferrer nofollow\">text</a></p>")]
            [InlineData("[text](badurl://www.asp.net)", "<p><a href=\"\" rel=\"noopener noreferrer nofollow\">text</a></p>")]
            [InlineData("![image](https://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" class=\"img-fluid\" alt=\"image\"></p>")]
            [InlineData("## License\n\tLicensed under the Apache License, Version 2.0 (the \"License\");", "<h3 id=\"license\">License</h3>\n<pre><code>Licensed under the Apache License, Version 2.0 (the \"License\");\n</code></pre>")]
            public void ConvertsMarkdownToHtml(string originalMd, string expectedHtml)
            {
                _featureFlagService.Setup(x => x.IsImageAllowlistEnabled()).Returns(false);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Fact]
            public void RewritesHttpImageUrlToHttps()
            {
                var originalMd = "![image](http://www.asp.net/fake.jpg)";
                var expectedHtml = "<p><img src=\"https://www.asp.net/fake.jpg\" class=\"img-fluid\" alt=\"image\"></p>";
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.True(readMeResult.ImagesRewritten);
            }

            [Fact]
            public void TestToHtmlWithPipeTable()
            {
                var originalMd = @"a | b
-- | -
0 | 1";

                var expectedHtml = "<table class=\"table\">\n<thead>\n<tr>\n<th>a</th>\n<th>b</th>\n</tr>\n</thead>\n<tbody>\n<tr>\n<td>0</td>\n<td>1</td>\n</tr>\n</tbody>\n</table>";
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Fact]
            public void TestToHtmlWithGridTable()
            {
                var originalMd = @"+---+---+
| a | b |
+===+===+
| 1 | 2 |
+---+---+
";

                var expectedHtml = "<table class=\"table\">\n<colgroup><col style=\"width: 50%\">\n<col style=\"width: 50%\">\n</colgroup><thead>\n<tr>\n<th>a</th>\n<th>b</th>\n</tr>\n</thead>\n<tbody>\n<tr>\n<td>1</td>\n<td>2</td>\n</tr>\n</tbody>\n</table>";
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Fact]
            public void TestToHtmlWithEmojiAndSmiley()
            {
                var originalMd = "This is a test with a :) and a :angry: smiley";

                var expectedHtml = "<p>This is a test with a 😃 and a 😠 smiley</p>";
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Fact]
            public void TestToHtmlWithTaskLists()
            {
                var originalMd = @"- [ ] Item1
- [x] Item2
- [ ] Item3
- Item4";

                var expectedHtml = "<ul class=\"contains-task-list\">\n<li class=\"task-list-item\"><input aria-label=\"Not completed\" disabled=\"disabled\" type=\"checkbox\"> Item1</li>\n<li class=\"task-list-item\">" +
                    "<input aria-label=\"Completed\" disabled=\"disabled\" type=\"checkbox\" checked=\"checked\"> Item2</li>\n<li class=\"task-list-item\"><input aria-label=\"Not completed\" disabled=\"disabled\" type=\"checkbox\"> " +
                    "Item3</li>\n<li>Item4</li>\n</ul>";
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Fact]
            public void TestToHtmlWithAdditionalList()
            {
                var originalMd = @"1.   First item

Some text

2.    Second item";

                var expectedHtml = "<ol>\n<li>First item</li>\n</ol>\n<p>Some text</p>\n<ol start=\"2\">\n<li>Second item</li>\n</ol>";
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Theory]
            [InlineData("This is a http://www.google.com URL and https://www.google.com", "<p>This is a <a href=\"https://www.google.com/\" rel=\"noopener noreferrer nofollow\">http://www.google.com</a> URL and <a href=\"https://www.google.com/\" rel=\"noopener noreferrer nofollow\">https://www.google.com</a></p>")]
            [InlineData("# This is a heading\n[Link](#this-is-a-heading)", "<h2 id=\"this-is-a-heading\">This is a heading</h2>\n<p><a href=\"#this-is-a-heading\" rel=\"noopener noreferrer nofollow\">Link</a></p>")]
            [InlineData("# Heading\n[Heading]", "<h2 id=\"heading\">Heading</h2>\n<p>[Heading]</p>")]
            public void TestToHtmlWithAutoLinks(string originalMd, string expectedHtml)
            {
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Theory]
            [InlineData("Hello ~~world~~", "<p>Hello <del>world</del></p>")]
            public void TestToHtmlWithStrikethrough(string originalMd, string expectedHtml)
            {
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Theory]
            [InlineData("# Heading", "<h2 id=\"heading\">Heading</h2>")]
            [InlineData("# This is a heading", "<h2 id=\"this-is-a-heading\">This is a heading</h2>")]
            [InlineData("# This .is a heading", "<h2 id=\"this-is-a-heading\">This .is a heading</h2>")]
            [InlineData("# This - is a &@! heading _ with . and ! -", "<h2 id=\"this---is-a--heading-_-with--and---\">This - is a &amp;@! heading _ with . and ! -</h2>")]
            [InlineData("# This is a *heading*", "<h2 id=\"this-is-a-heading\">This is a <em>heading</em></h2>")]
            [InlineData("# This is a [heading](https://www.google.com)", "<h2 id=\"this-is-a-heading\">This is a <a href=\"https://www.google.com/\" rel=\"noopener noreferrer nofollow\">heading</a></h2>")]
            [InlineData("# Heading\n# Heading", "<h2 id=\"heading\">Heading</h2>\n<h2 id=\"heading-1\">Heading</h2>")]
            [InlineData("# 1.0 This is a heading", "<h2 id=\"10-this-is-a-heading\">1.0 This is a heading</h2>")]
            [InlineData("# 1.0 & ^ % *\n# 1.0 & ^ % *", "<h2 id=\"10----\">1.0 &amp; ^ % *</h2>\n<h2 id=\"10-----1\">1.0 &amp; ^ % *</h2>")]
            public void TestToHtmlWithAutoIdentifiers(string originalMd, string expectedHtml)
            {
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Theory]
            [InlineData("> [!NOTE]\n> This is a note", "<div class=\"markdown-alert markdown-alert-note alert alert-primary\">\n<p class=\"mb-0\">This is a note</p>\n</div>")]
            [InlineData("> [!TIP]\n> This is a tip", "<div class=\"markdown-alert markdown-alert-tip alert alert-success\">\n<p class=\"mb-0\">This is a tip</p>\n</div>")]
            [InlineData("> [!IMPORTANT]\n> This is a important", "<div class=\"markdown-alert markdown-alert-important alert alert-info\">\n<p class=\"mb-0\">This is a important</p>\n</div>")]
            [InlineData("> [!WARNING]\n> This is a warning", "<div class=\"markdown-alert markdown-alert-warning alert alert-warning\">\n<p class=\"mb-0\">This is a warning</p>\n</div>")]
            [InlineData("> [!CAUTION]\n> This is a caution", "<div class=\"markdown-alert markdown-alert-caution alert alert-danger\">\n<p class=\"mb-0\">This is a caution</p>\n</div>")]
            public void TestToHtmlWithAlertBlocks(string originalMd, string expectedHtml)
            {
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Theory]
            [InlineData("H<sub>2</sub>O", "<p>H<sub>2</sub>O</p>")]
            [InlineData("x<sup>2</sup>", "<p>x<sup>2</sup></p>")]
            [InlineData("line1<br>line2", "<p>line1<br>line2</p>")]
            [InlineData("<p align=\"center\">centered</p>", "<p align=\"center\">centered</p>")]
            [InlineData("<div>content</div>", "<div>content</div>")]
            public void RendersAllowedHtmlTags(string originalMd, string expectedHtml)
            {
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Theory]
            [InlineData("<a href=\"https://example.com\">link</a>", "<p><a href=\"https://example.com\" rel=\"noopener noreferrer nofollow\">link</a></p>")]
            [InlineData("<a href=\"http://example.com\">link</a>", "<p><a href=\"https://example.com/\" rel=\"noopener noreferrer nofollow\">link</a></p>")]
            public void AddsRelAndRewritesHttpOnRawHtmlLinks(string originalMd, string expectedHtml)
            {
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }

            [Fact]
            public void RewritesHttpToHttpsOnRawHtmlImages()
            {
                var originalMd = "<img src=\"http://example.com/image.png\" alt=\"test\">";
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Contains("src=\"https://example.com/image.png\"", readMeResult.Content);
                Assert.True(readMeResult.ImagesRewritten);
            }

            [Fact]
            public void ImagesRewrittenIsFalseWhenNoHttpImages()
            {
                var originalMd = "![image](https://example.com/image.png)";
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.False(readMeResult.ImagesRewritten);
            }
        }
    }
}

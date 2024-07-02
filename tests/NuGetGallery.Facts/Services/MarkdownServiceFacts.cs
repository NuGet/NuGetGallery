// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            private readonly Mock<IImageDomainValidator> _imageDomainValidator;

            public GetReadMeHtmlMethod()
            {
                _featureFlagService = new Mock<IFeatureFlagService>();
                _imageDomainValidator = new Mock<IImageDomainValidator>();
                _markdownService = new MarkdownService(_featureFlagService.Object, _imageDomainValidator.Object);
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
            [InlineData("<script>alert('test')</script>", "<p>&lt;script&gt;alert('test')&lt;/script&gt;</p>", true)]
            [InlineData("<script>alert('test')</script>", "<p>&lt;script&gt;alert('test')&lt;/script&gt;</p>", false)]
            [InlineData("<img src=\"javascript:alert('test');\">", "<p>&lt;img src=&quot;javascript:alert('test');&quot;&gt;</p>", true)]
            [InlineData("<img src=\"javascript:alert('test');\">", "<p>&lt;img src=&quot;javascript:alert('test');&quot;&gt;</p>", false)]
            [InlineData("<a href=\"javascript:alert('test');\">", "<p>&lt;a href=&quot;javascript:alert('test');&quot;&gt;</p>", true)]
            [InlineData("<a href=\"javascript:alert('test');\">", "<p>&lt;a href=&quot;javascript:alert('test');&quot;&gt;</p>", false)]
            public void EncodesHtmlInMarkdown(string originalMd, string expectedHtml, bool isMarkdigMdRenderingEnabled)
            {
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(isMarkdigMdRenderingEnabled);
                Assert.Equal(expectedHtml, _markdownService.GetHtmlFromMarkdown(originalMd).Content);
            }

            [Theory]
            [InlineData("# Heading", "<h1 id=\"heading\">Heading</h1>", true, 0)]
            [InlineData("# Heading", "<h1>Heading</h1>", false, 0)]
            [InlineData("# Heading", "<h2 id=\"heading\">Heading</h2>", true, 1)]
            [InlineData("# Heading", "<h2>Heading</h2>", false, 1)]
            [InlineData("# Heading", "<h6 id=\"heading\">Heading</h6>", true, 6)]
            [InlineData("# Heading", "<h6>Heading</h6>", false, 6)]
            [InlineData("# Heading", "<h6 id=\"heading\">Heading</h6>", true, 7)]
            [InlineData("# Heading", "<h6>Heading</h6>", false, 7)]
            [InlineData("# Heading", "<h6 id=\"heading\">Heading</h6>", true, 5)]
            [InlineData("# Heading", "<h6>Heading</h6>", false, 5)]
            public void EncodesHtmlInMarkdownWithAdaptiveHeader(string originalMd, string expectedHtml, bool isMarkdigMdRenderingEnabled, int incrementHeadersBy)
            {
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(isMarkdigMdRenderingEnabled);
                Assert.Equal(expectedHtml, _markdownService.GetHtmlFromMarkdown(originalMd, incrementHeadersBy).Content);
            }

            [Theory]
            [InlineData("# Heading", "<h2 id=\"heading\">Heading</h2>", false, true)]
            [InlineData("# Heading", "<h2>Heading</h2>", false, false)]
            [InlineData("<!-- foo --> <!-- foo \n bar --> baz", "<p>baz</p>", false, true)]
            [InlineData("<!-- foo --> <!-- foo \n bar --> baz", "<p>baz</p>", false, false)]
            [InlineData("\ufeff# Heading with BOM", "<h2 id=\"heading-with-bom\">Heading with BOM</h2>", false, true)]
            [InlineData("\ufeff# Heading with BOM", "<h2>Heading with BOM</h2>", false, false)]
            [InlineData("- List", "<ul>\n<li>List</li>\n</ul>", false, true)]
            [InlineData("- List", "<ul>\r\n<li>List</li>\r\n</ul>", false, false)]
            [InlineData("This is a paragraph\nwithout a break inside", "<p>This is a paragraph\nwithout a break inside</p>", false, true)]
            [InlineData("This is a paragraph\r\nwithout a break inside", "<p>This is a paragraph\r\nwithout a break inside</p>", false, false)]
            [InlineData("soft line break line1  \nline2  \nline3  ", "<p>soft line break line1<br />\nline2<br />\nline3</p>", false, true)]
            [InlineData("soft line break line1  \r\nline2  \r\nline3  ", "<p>soft line break line1<br />\r\nline2<br />\r\nline3</p>", false, false)]
            [InlineData("hard line break line1\n\nline2\n\nline3", "<p>hard line break line1</p>\n<p>line2</p>\n<p>line3</p>", false, true)]
            [InlineData("hard line break line1\r\n\r\nline2\r\n\r\nline3", "<p>hard line break line1</p>\r\n<p>line2</p>\r\n<p>line3</p>", false, false)]
            [InlineData("[text](http://www.test.com)", "<p><a href=\"http://www.test.com/\" rel=\"noopener noreferrer nofollow\">text</a></p>", false, true)]
            [InlineData("[text](http://www.test.com)", "<p><a href=\"http://www.test.com/\" rel=\"noopener noreferrer nofollow\">text</a></p>", false, false)]
            [InlineData("[text](javascript:alert('hi'))", "<p><a href=\"\" rel=\"noopener noreferrer nofollow\">text</a></p>", false, true)]
            [InlineData("[text](javascript:alert('hi'))", "<p><a href=\"\" rel=\"noopener noreferrer nofollow\">text</a></p>", false, false)]
            [InlineData("> <text>Blockquote</text>", "<blockquote class=\"blockquote\">\n<p>&lt;text&gt;Blockquote&lt;/text&gt;</p>\n</blockquote>", false, true)]
            [InlineData("> <text>Blockquote</text>", "<blockquote>\r\n<p>&lt;text&gt;Blockquote&lt;/text&gt;</p>\r\n</blockquote>", false, false)]
            [InlineData("> > <text>Blockquote</text>", "<blockquote class=\"blockquote\">\n<blockquote class=\"blockquote\">\n<p>&lt;text&gt;Blockquote&lt;/text&gt;</p>\n</blockquote>\n</blockquote>", false, true)]
            [InlineData("> > <text>Blockquote</text>", "<blockquote>\r\n<p>&gt; &lt;text&gt;Blockquote&lt;/text&gt;</p>\r\n</blockquote>", false, false)]
            [InlineData("[text](http://www.asp.net)", "<p><a href=\"https://www.asp.net/\" rel=\"noopener noreferrer nofollow\">text</a></p>", false, true)]
            [InlineData("[text](http://www.asp.net)", "<p><a href=\"https://www.asp.net/\" rel=\"noopener noreferrer nofollow\">text</a></p>", false, false)]
            [InlineData("[text](badurl://www.asp.net)", "<p><a href=\"\" rel=\"noopener noreferrer nofollow\">text</a></p>", false, true)]
            [InlineData("[text](badurl://www.asp.net)", "<p><a href=\"\" rel=\"noopener noreferrer nofollow\">text</a></p>", false, false)]
            [InlineData("![image](http://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" class=\"img-fluid\" alt=\"image\" /></p>", true, true)]
            [InlineData("![image](http://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" alt=\"image\" /></p>", true, false)]
            [InlineData("![image](https://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" class=\"img-fluid\" alt=\"image\" /></p>", false, true)]
            [InlineData("![image](https://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" alt=\"image\" /></p>", false, false)]
            [InlineData("![image](http://www.otherurl.net/fake.jpg)", "<p><img src=\"https://www.otherurl.net/fake.jpg\" class=\"img-fluid\" alt=\"image\" /></p>", true, true)]
            [InlineData("![image](http://www.otherurl.net/fake.jpg)", "<p><img src=\"https://www.otherurl.net/fake.jpg\" alt=\"image\" /></p>", true, false)]
            [InlineData("![](http://www.otherurl.net/fake.jpg)", "<p><img src=\"https://www.otherurl.net/fake.jpg\" alt=\"\" /></p>", true, false)]
            [InlineData("![](http://www.otherurl.net/fake.jpg)", "<p><img src=\"https://www.otherurl.net/fake.jpg\" class=\"img-fluid\" alt=\"alternate text is missing from this package README image\" /></p>", true, true)]
            [InlineData("## License\n\tLicensed under the Apache License, Version 2.0 (the \"License\");", "<h3 id=\"license\">License</h3>\n<pre><code>Licensed under the Apache License, Version 2.0 (the &quot;License&quot;);\n</code></pre>", false, true)]
            [InlineData("## License\n\tLicensed under the Apache License, Version 2.0 (the \"License\");", "<h3 id=\"license\">License</h3>\n<pre><code>Licensed under the Apache License, Version 2.0 (the &quot;License&quot;);\n</code></pre>", false, true)]
            public void ConvertsMarkdownToHtml(string originalMd, string expectedHtml, bool imageRewriteExpected, bool isMarkdigMdRenderingEnabled)
            {
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(isMarkdigMdRenderingEnabled);
                _featureFlagService.Setup(x => x.IsImageAllowlistEnabled()).Returns(false);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.Equal(imageRewriteExpected, readMeResult.ImagesRewritten);
            }

            [Theory]
            [InlineData(true, "<p><img src=\"https://api.codacy.com/example/image.svg\" class=\"img-fluid\" alt=\"image\" /></p>")]
            [InlineData(false, "<p><img src=\"https://api.codacy.com/example/image.svg\" alt=\"image\" /></p>")]
            public void ConvertsMarkdownToHtmlWithImageDisaplyed(bool isMarkdigMdRenderingEnabled, string expectedHtml)
            {
                string imageUrl = "https://api.codacy.com/example/image.svg";
                string originalMd = "![image](https://api.codacy.com/example/image.svg)";

                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(isMarkdigMdRenderingEnabled);
                _featureFlagService.Setup(x => x.IsImageAllowlistEnabled()).Returns(true);
                _imageDomainValidator.Setup(x => x.TryPrepareImageUrlForRendering(imageUrl, out imageUrl)).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.False(readMeResult.ImageSourceDisallowed);
            }

            [Theory]
            [InlineData(true, "<p><img src=\"\" class=\"img-fluid\" alt=\"image\" /></p>")]
            [InlineData(false, "<p><img src=\"\" alt=\"image\" /></p>")]
            public void ConvertsMarkdownToHtmlWithoutImageDisaplyed(bool isMarkdigMdRenderingEnabled, string expectedHtml)
            {
                string imageUrl = "https://nuget.org/example/image.svg";
                string originalMd = "![image](https://nuget.org/example/image.svg)";
                string outUrl = null;

                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(isMarkdigMdRenderingEnabled);
                _featureFlagService.Setup(x => x.IsImageAllowlistEnabled()).Returns(true);
                _imageDomainValidator.Setup(x => x.TryPrepareImageUrlForRendering(imageUrl, out outUrl)).Returns(false);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.True(readMeResult.ImageSourceDisallowed);
            }

            [Fact]
            public void TestToHtmlWithPipeTable() 
            {
                var originalMd = @"a | b
-- | -
0 | 1";

                var expectedHtml = "<table class=\"table\">\n<thead>\n<tr>\n<th>a</th>\n<th>b</th>\n</tr>\n</thead>\n<tbody>\n<tr>\n<td>0</td>\n<td>1</td>\n</tr>\n</tbody>\n</table>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.False(readMeResult.ImagesRewritten);
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

                var expectedHtml = "<table class=\"table\">\n<col style=\"width:50%\" />\n<col style=\"width:50%\" />\n<thead>\n<tr>\n<th>a</th>\n<th>b</th>\n</tr>\n</thead>\n<tbody>\n<tr>\n<td>1</td>\n<td>2</td>\n</tr>\n</tbody>\n</table>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.False(readMeResult.ImagesRewritten);
            }

            [Fact]
            public void TestToHtmlWithEmojiAndSmiley()
            {
                var originalMd = "This is a test with a :) and a :angry: smiley";

                var expectedHtml = "<p>This is a test with a 😃 and a 😠 smiley</p>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.False(readMeResult.ImagesRewritten);
            }

            [Fact]
            public void TestToHtmlWithTaskLists()
            {
                var originalMd = @"- [ ] Item1
- [x] Item2
- [ ] Item3
- Item4";

                var expectedHtml = "<ul class=\"contains-task-list\">\n<li class=\"task-list-item\"><input disabled=\"disabled\" type=\"checkbox\" /> Item1</li>\n<li class=\"task-list-item\">" +
                    "<input disabled=\"disabled\" type=\"checkbox\" checked=\"checked\" /> Item2</li>\n<li class=\"task-list-item\"><input disabled=\"disabled\" type=\"checkbox\" /> " +
                    "Item3</li>\n<li>Item4</li>\n</ul>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.False(readMeResult.ImagesRewritten);
            }

            [Fact]
            public void TestToHtmlWithAdditionalList()
            {
                var originalMd = @"1.   First item

Some text

2.    Second item";

                var expectedHtml = "<ol>\n<li>First item</li>\n</ol>\n<p>Some text</p>\n<ol start=\"2\">\n<li>Second item</li>\n</ol>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.False(readMeResult.ImagesRewritten);
            }

            [Theory]
            [InlineData("This is a http://www.google.com URL and https://www.google.com", "<p>This is a <a href=\"http://www.google.com/\" rel=\"noopener noreferrer nofollow\">http://www.google.com</a> URL and <a href=\"https://www.google.com/\" rel=\"noopener noreferrer nofollow\">https://www.google.com</a></p>")]
            [InlineData("# This is a heading\n[Link](#this-is-a-heading)", "<h2 id=\"this-is-a-heading\">This is a heading</h2>\n<p><a href=\"#this-is-a-heading\" rel=\"noopener noreferrer nofollow\">Link</a></p>")]
            [InlineData("# Heading\n[Heading]", "<h2 id=\"heading\">Heading</h2>\n<p><a href=\"#heading\" rel=\"noopener noreferrer nofollow\">Heading</a></p>")]
            public void TestToHtmlWithAutoLinks(string originalMd, string expectedHtml)
            {
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.False(readMeResult.ImagesRewritten);
            }

            [Theory]
            [InlineData("Hello ~~world~~", "<p>Hello <del>world</del></p>")]
            public void TestToHtmlWithStrikethrough(string originalMd, string expectedHtml)
            {
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.False(readMeResult.ImagesRewritten);
            }

            [Theory]
            [InlineData("# Heading", "<h2 id=\"heading\">Heading</h2>")]
            [InlineData("# This is a heading", "<h2 id=\"this-is-a-heading\">This is a heading</h2>")]
            [InlineData("# This - is a &@! heading _ with . and ! -", "<h2 id=\"this-is-a-heading_with.and\">This - is a &amp;@! heading _ with . and ! -</h2>")]
            [InlineData("# This is a *heading*", "<h2 id=\"this-is-a-heading\">This is a <em>heading</em></h2>")]
            [InlineData("# This is a [heading](https://www.google.com)", "<h2 id=\"this-is-a-heading\">This is a <a href=\"https://www.google.com/\" rel=\"noopener noreferrer nofollow\">heading</a></h2>")]
            [InlineData("# Heading\n# Heading", "<h2 id=\"heading\">Heading</h2>\n<h2 id=\"heading-1\">Heading</h2>")]
            [InlineData("# 1.0 This is a heading", "<h2 id=\"this-is-a-heading\">1.0 This is a heading</h2>")]
            [InlineData("# 1.0 & ^ % *\n# 1.0 & ^ % *", "<h2 id=\"section\">1.0 &amp; ^ % *</h2>\n<h2 id=\"section-1\">1.0 &amp; ^ % *</h2>")]
            public void TestToHtmlWithAutoIdentifiers(string originalMd, string expectedHtml)
            {
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
            }
        }
    }
}

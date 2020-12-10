﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Core.Objects;
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

            public GetReadMeHtmlMethod()
            {
                _featureFlagService = new Mock<IFeatureFlagService>();
                _markdownService = new MarkdownService(_featureFlagService.Object);
            }

            [Fact]
            public void ThrowsArgumentOutOfRangeExceptionForNegativeParameterValue()
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => _markdownService.GetHtmlFromMarkdown("markdown file test", -1));
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
            [InlineData("# Heading", "<h1>Heading</h1>", true, 0)]
            [InlineData("# Heading", "<h1>Heading</h1>", false, 0)]
            [InlineData("# Heading", "<h2>Heading</h2>", true, 1)]
            [InlineData("# Heading", "<h2>Heading</h2>", false, 1)]
            [InlineData("# Heading", "<h6>Heading</h6>", true, 6)]
            [InlineData("# Heading", "<h6>Heading</h6>", false, 6)]
            [InlineData("# Heading", "<h6>Heading</h6>", true, 7)]
            [InlineData("# Heading", "<h6>Heading</h6>", false, 7)]
            [InlineData("# Heading", "<h6>Heading</h6>", true, 5)]
            [InlineData("# Heading", "<h6>Heading</h6>", false, 5)]
            public void EncodesHtmlInMarkdownWithAdaptiveHeader(string originalMd, string expectedHtml, bool isMarkdigMdRenderingEnabled, int incrementHeadersBy)
            {
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(isMarkdigMdRenderingEnabled);
                Assert.Equal(expectedHtml, _markdownService.GetHtmlFromMarkdown(originalMd, incrementHeadersBy).Content);
            }

            [Theory]
            [InlineData("# Heading", "<h2>Heading</h2>", false, true)]
            [InlineData("# Heading", "<h2>Heading</h2>", false, false)]
            [InlineData("\ufeff# Heading with BOM", "<h2>Heading with BOM</h2>", false, true)]
            [InlineData("\ufeff# Heading with BOM", "<h2>Heading with BOM</h2>", false, false)]
            [InlineData("- List", "<ul>\n<li>List</li>\n</ul>", false, true)]
            [InlineData("- List", "<ul>\r\n<li>List</li>\r\n</ul>", false, false)]
            [InlineData("[text](http://www.test.com)", "<p><a href=\"http://www.test.com/\" rel=\"nofollow\">text</a></p>", false, true)]
            [InlineData("[text](http://www.test.com)", "<p><a href=\"http://www.test.com/\" rel=\"nofollow\">text</a></p>", false, false)]
            [InlineData("[text](javascript:alert('hi'))", "<p><a href=\"\" rel=\"nofollow\">text</a></p>", false, true)]
            [InlineData("[text](javascript:alert('hi'))", "<p><a href=\"\" rel=\"nofollow\">text</a></p>", false, false)]
            [InlineData("> <text>Blockquote</text>", "<blockquote>\n<p>&lt;text&gt;Blockquote&lt;/text&gt;</p>\n</blockquote>", false, true)]
            [InlineData("> <text>Blockquote</text>", "<blockquote>\r\n<p>&lt;text&gt;Blockquote&lt;/text&gt;</p>\r\n</blockquote>", false, false)]
            [InlineData("> > <text>Blockquote</text>", "<blockquote>\n<blockquote>\n<p>&lt;text&gt;Blockquote&lt;/text&gt;</p>\n</blockquote>\n</blockquote>", false, true)]
            [InlineData("> > <text>Blockquote</text>", "<blockquote>\r\n<p>&gt; &lt;text&gt;Blockquote&lt;/text&gt;</p>\r\n</blockquote>", false, false)]
            [InlineData("[text](http://www.asp.net)", "<p><a href=\"https://www.asp.net/\" rel=\"nofollow\">text</a></p>", false, true)]
            [InlineData("[text](http://www.asp.net)", "<p><a href=\"https://www.asp.net/\" rel=\"nofollow\">text</a></p>", false, false)]
            [InlineData("[text](badurl://www.asp.net)", "<p><a href=\"\" rel=\"nofollow\">text</a></p>", false, true)]
            [InlineData("[text](badurl://www.asp.net)", "<p><a href=\"\" rel=\"nofollow\">text</a></p>", false, false)]
            [InlineData("![image](http://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" alt=\"image\" /></p>", true, true)]
            [InlineData("![image](http://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" alt=\"image\" /></p>", true, false)]
            [InlineData("![image](https://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" alt=\"image\" /></p>", false, true)]
            [InlineData("![image](https://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" alt=\"image\" /></p>", false, false)]
            [InlineData("![image](http://www.otherurl.net/fake.jpg)", "<p><img src=\"https://www.otherurl.net/fake.jpg\" alt=\"image\" /></p>", true, true)]
            [InlineData("![image](http://www.otherurl.net/fake.jpg)", "<p><img src=\"https://www.otherurl.net/fake.jpg\" alt=\"image\" /></p>", true, false)]
            [InlineData("## License\n\tLicensed under the Apache License, Version 2.0 (the \"License\");", "<h3>License</h3>\n<pre><code>Licensed under the Apache License, Version 2.0 (the &quot;License&quot;);\n</code></pre>", false, true)]
            [InlineData("## License\n\tLicensed under the Apache License, Version 2.0 (the \"License\");", "<h3>License</h3>\n<pre><code>Licensed under the Apache License, Version 2.0 (the &quot;License&quot;);\n</code></pre>", false, true)]
            public void ConvertsMarkdownToHtml(string originalMd, string expectedHtml, bool imageRewriteExpected, bool isMarkdigMdRenderingEnabled)
            {
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(isMarkdigMdRenderingEnabled);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.Equal(imageRewriteExpected, readMeResult.ImagesRewritten);
            }

            [Fact]
            public void TestToHtmlWithExtension()
            {
                var originalMd = "This is a paragraph\r\n with a break inside";
                var expectedHtml = "<p>This is a paragraph<br />\nwith a break inside</p>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.Equal(false, readMeResult.ImagesRewritten);
            }

            [Fact]
            public void TestToHtmlWithPipeTable() 
            {
                var originalMd = @"a | b
-- | -
0 | 1";

                var expectedHtml = "<table>\n<thead>\n<tr>\n<th>a</th>\n<th>b</th>\n</tr>\n</thead>\n<tbody>\n<tr>\n<td>0</td>\n<td>1</td>\n</tr>\n</tbody>\n</table>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.Equal(false, readMeResult.ImagesRewritten);
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

                var expectedHtml = "<table>\n<col style=\"width:50%\" />\n<col style=\"width:50%\" />\n<thead>\n<tr>\n<th>a</th>\n<th>b</th>\n</tr>\n</thead>\n<tbody>\n<tr>\n<td>1</td>\n<td>2</td>\n</tr>\n</tbody>\n</table>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.Equal(false, readMeResult.ImagesRewritten);
            }

            [Fact]
            public void TestToHtmlWithEmojiAndSmiley()
            {
                var originalMd = "This is a test with a :) and a :angry: smiley";

                var expectedHtml = "<p>This is a test with a 😃 and a 😠 smiley</p>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.Equal(false, readMeResult.ImagesRewritten);
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
                Assert.Equal(false, readMeResult.ImagesRewritten);
            }

            [Fact]
            public void TestToHtmlWithAddtionalList()
            {
                var originalMd = @"1.   First item

Some text

2.    Second item";

                var expectedHtml = "<ol>\n<li>First item</li>\n</ol>\n<p>Some text</p>\n<ol start=\"2\">\n<li>Second item</li>\n</ol>";
                _featureFlagService.Setup(x => x.IsMarkdigMdRenderingEnabled()).Returns(true);
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.Equal(false, readMeResult.ImagesRewritten);
            }
        }
    }
}

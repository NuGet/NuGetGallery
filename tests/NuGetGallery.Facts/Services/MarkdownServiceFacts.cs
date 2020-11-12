// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            [InlineData("# Heading", "<h2>Heading</h2>", false, true)]
            [InlineData("# Heading", "<h2>Heading</h2>", false, false)]
            [InlineData("\ufeff# Heading with BOM", "<h2>Heading with BOM</h2>", false, true)]
            [InlineData("\ufeff# Heading with BOM", "<h2>Heading with BOM</h2>", false, false)]
            [InlineData("- List", "<ul>\r\n<li>List</li>\r\n</ul>", false, true)]
            [InlineData("- List", "<ul>\r\n<li>List</li>\r\n</ul>", false, false)]
            [InlineData("[text](http://www.test.com)", "<p><a href=\"http://www.test.com/\" rel=\"nofollow\">text</a></p>", false, true)]
            [InlineData("[text](http://www.test.com)", "<p><a href=\"http://www.test.com/\" rel=\"nofollow\">text</a></p>", false, false)]
            [InlineData("[text](javascript:alert('hi'))", "<p><a href=\"\" rel=\"nofollow\">text</a></p>", false, true)]
            [InlineData("[text](javascript:alert('hi'))", "<p><a href=\"\" rel=\"nofollow\">text</a></p>", false, false)]
            [InlineData("> <text>Blockquote</text>", "<blockquote>\r\n<p>&lt;text&gt;Blockquote&lt;/text&gt;</p>\r\n</blockquote>", false, true)]
            [InlineData("> <text>Blockquote</text>", "<blockquote>\r\n<p>&lt;text&gt;Blockquote&lt;/text&gt;</p>\r\n</blockquote>", false, false)]
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
            [InlineData("![image](http://www.otherurl.net/fake.jpg)", "<p><img src=\"https://www.otherurl.net/fake.jpg\" alt=\"image\" /></p>", false, true)]
            [InlineData("![image](http://www.otherurl.net/fake.jpg)", "<p><img src=\"https://www.otherurl.net/fake.jpg\" alt=\"image\" /></p>", false, false)]
            public void ConvertsMarkdownToHtml(string originalMd, string expectedHtml, bool imageRewriteExpected, bool isMarkdigMdRenderingEnabled)
            {
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.Equal(imageRewriteExpected, readMeResult.ImagesRewritten);
            }
        }
    }
}

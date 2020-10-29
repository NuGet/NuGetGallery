// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery
{
    public class MarkdownServiceFacts
    {
       
        public class GetReadMeHtmlMethod
        {
            private readonly MarkdownService _markdownService;

            public GetReadMeHtmlMethod()
            {
                _markdownService = new MarkdownService();
            }

            [Theory]
            [InlineData("<script>alert('test')</script>", "<p>&lt;script&gt;alert('test')&lt;/script&gt;</p>")]
            [InlineData("<img src=\"javascript:alert('test');\">", "<p>&lt;img src=&quot;javascript:alert('test');&quot;&gt;</p>")]
            [InlineData("<a href=\"javascript:alert('test');\">", "<p>&lt;a href=&quot;javascript:alert('test');&quot;&gt;</p>")]
            public void EncodesHtmlInMarkdown(string originalMd, string expectedHtml)
            {
                Assert.Equal(expectedHtml, _markdownService.GetHtmlFromMarkdown(originalMd).Content);
            }

            [Theory]
            [InlineData("# Heading", "<h2>Heading</h2>", false)]
            [InlineData("\ufeff# Heading with BOM", "<h2>Heading with BOM</h2>", false)]
            [InlineData("- List", "<ul>\n<li>List</li>\n</ul>", false)]
            [InlineData("[text](http://www.test.com)", "<p><a href=\"http://www.test.com/\" rel=\"nofollow\">text</a></p>", false)]
            [InlineData("[text](javascript:alert('hi'))", "<p><a href=\"\" rel=\"nofollow\">text</a></p>", false)]
            [InlineData("> <text>Blockquote</text>", "<blockquote>\n<p>&lt;text&gt;Blockquote&lt;/text&gt;</p>\n</blockquote>", false)]
            [InlineData("[text](http://www.asp.net)", "<p><a href=\"https://www.asp.net/\" rel=\"nofollow\">text</a></p>", false)]
            [InlineData("[text](badurl://www.asp.net)", "<p><a href=\"\" rel=\"nofollow\">text</a></p>", false)]
            [InlineData("![image](http://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" alt=\"image\" /></p>", true)]
            [InlineData("![image](https://www.asp.net/fake.jpg)", "<p><img src=\"https://www.asp.net/fake.jpg\" alt=\"image\" /></p>", false)]
            [InlineData("![image](http://www.otherurl.net/fake.jpg)", "<p><img src=\"https://www.otherurl.net/fake.jpg\" alt=\"image\" /></p>", true)]
            public void ConvertsMarkdownToHtml(string originalMd, string expectedHtml, bool imageRewriteExpected)
            {
                var readMeResult = _markdownService.GetHtmlFromMarkdown(originalMd);
                Assert.Equal(expectedHtml, readMeResult.Content);
                Assert.Equal(imageRewriteExpected, readMeResult.ImagesRewritten);
            }
        }
    }
}

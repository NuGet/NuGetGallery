﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class HtmlExtensionsFacts
    {
        public class ThePreFormattedTextMethod
        {
            [Fact]
            public void ConvertsNewLinesToBrTags()
            {
                // Arrange
                var htmlHelper = CreateHtmlHelper(new ViewDataDictionary());
                var input = "first line\nsecond line";

                // Act
                var output = htmlHelper.PreFormattedText(input);

                // Assert
                var html = output.ToHtmlString();
                Assert.Equal("first line<br />second line", html);
            }

            [Fact]
            public void ConvertsSpacesToNbsp()
            {
                // Arrange
                var htmlHelper = CreateHtmlHelper(new ViewDataDictionary());
                var input = "Five spaces:     END";

                // Act
                var output = htmlHelper.PreFormattedText(input);

                // Assert
                var html = output.ToHtmlString();
                Assert.Equal("Five spaces: &nbsp;&nbsp;&nbsp;&nbsp;END", html);
            }

            [Fact]
            public void EncodesHtml()
            {
                // Arrange
                var htmlHelper = CreateHtmlHelper(new ViewDataDictionary());
                var input = "<script>alert('foo!')</script>";

                // Act
                var output = htmlHelper.PreFormattedText(input);

                // Assert
                var html = output.ToHtmlString();
                Assert.Equal("&lt;script&gt;alert(&#39;foo!&#39;)&lt;/script&gt;", html);
            }

            [Theory]
            [InlineData("My site is https://www.nuget.org.", "My site is <a href=\"https://www.nuget.org/\" rel=\"nofollow\">https://www.nuget.org/</a>.")]
            [InlineData("My site is https://www.nuget.org!", "My site is <a href=\"https://www.nuget.org/\" rel=\"nofollow\">https://www.nuget.org/</a>!")]
            [InlineData("My site is http://www.nuget.org", "My site is <a href=\"http://www.nuget.org/\" rel=\"nofollow\">http://www.nuget.org/</a>")]
            [InlineData("My site is http://www.nuget.org/sub/path/", "My site is <a href=\"http://www.nuget.org/sub/path/\" rel=\"nofollow\">http://www.nuget.org/sub/path/</a>")]
            [InlineData("My site is https://www.nuget.org/packages.", "My site is <a href=\"https://www.nuget.org/packages\" rel=\"nofollow\">https://www.nuget.org/packages</a>.")]
            [InlineData("My site is http://www.nuget.org/?foo&bar=2#a", "My site is <a href=\"http://www.nuget.org/?foo&amp;bar=2#a\" rel=\"nofollow\">http://www.nuget.org/?foo&amp;bar=2#a</a>")]
            [InlineData("My site is http://www.nuget.org/?foo[]=a", "My site is <a href=\"http://www.nuget.org/?foo\" rel=\"nofollow\">http://www.nuget.org/?foo</a>[]=a")]
            [InlineData("http://a.com http://b.com", "<a href=\"http://a.com/\" rel=\"nofollow\">http://a.com/</a> <a href=\"http://b.com/\" rel=\"nofollow\">http://b.com/</a>")]
            [InlineData("http://www.nuget.org/ is my site.", "<a href=\"http://www.nuget.org/\" rel=\"nofollow\">http://www.nuget.org/</a> is my site.")]
            [InlineData("\"http://www.nuget.org/\" is my site.", "&quot;<a href=\"http://www.nuget.org/\" rel=\"nofollow\">http://www.nuget.org/</a>&quot; is my site.")]
            [InlineData("\'http://www.nuget.org/\' is my site.", "&#39;<a href=\"http://www.nuget.org/\" rel=\"nofollow\">http://www.nuget.org/</a>&#39; is my site.")]
            [InlineData("http://www.nuget.org, is my site.", "<a href=\"http://www.nuget.org/\" rel=\"nofollow\">http://www.nuget.org/</a>, is my site.")]
            [InlineData("(http://www.nuget.org) is my site.", "(<a href=\"http://www.nuget.org/\" rel=\"nofollow\">http://www.nuget.org/</a>) is my site.")]
            [InlineData("http://www.nuget.org; is my site.", "<a href=\"http://www.nuget.org/\" rel=\"nofollow\">http://www.nuget.org/</a>; is my site.")]
            [InlineData("http://www.nuget.org- is my site.", "<a href=\"http://www.nuget.org/\" rel=\"nofollow\">http://www.nuget.org/</a>- is my site.")]
            [InlineData("http://www.github.com/nuget is my site.", "<a href=\"https://www.github.com/nuget\" rel=\"nofollow\">https://www.github.com/nuget</a> is my site.")]
            [InlineData("My site is http://www.asp.net best site ever!", "My site is <a href=\"https://www.asp.net/\" rel=\"nofollow\">https://www.asp.net/</a> best site ever!")]
            [InlineData("My site is http:////github.com bad url", "My site is http:////github.com bad url")]
            [InlineData("Im using github pages! http://mypage.github.com/stuff.", "Im using github pages! <a href=\"https://mypage.github.com/stuff\" rel=\"nofollow\">https://mypage.github.com/stuff</a>.")]
            public void ConvertsUrlsToLinks(string input, string expected)
            {
                // Arrange
                var htmlHelper = CreateHtmlHelper(new ViewDataDictionary());

                // Act
                var output = htmlHelper.PreFormattedText(input);

                // Assert
                var html = output.ToHtmlString();
                Assert.Equal(expected, html);
            }
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/2499734
        /// </summary>
        private static HtmlHelper CreateHtmlHelper(ViewDataDictionary vd)
        {
            var mockViewContext = new Mock<ViewContext>(
                new ControllerContext(
                    new Mock<HttpContextBase>().Object,
                    new RouteData(),
                    new Mock<ControllerBase>().Object),
                new Mock<IView>().Object,
                vd,
                new TempDataDictionary(),
                new StringWriter());

            var mockViewDataContainer = new Mock<IViewDataContainer>();
            mockViewDataContainer.Setup(v => v.ViewData).Returns(vd);

            return new HtmlHelper(mockViewContext.Object, mockViewDataContainer.Object);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGetGallery;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class ReadMeHelperFacts
    {
        [Fact]
        public void HasReadMe_WhenRequestIsNull_ReturnsFalse()
        {
            Assert.False(ReadMeHelper.HasReadMe(null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("InvalidType")]
        public void HasReadMe_WhenTypeIsUnknown_ReturnsFalse(string readMeType)
        {
            Assert.False(ReadMeHelper.HasReadMe(new ReadMeRequest() { ReadMeType = readMeType }));
        }
        
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("github.com")]
        public void HasReadMe_WhenUrlIsInvalid_ReturnsFalse(string invalidUrl)
        {
            var request = new ReadMeRequest
            {
                ReadMeType = "Url",
                ReadMeUrl = invalidUrl
            };

            Assert.False(ReadMeHelper.HasReadMe(request));
        }

        [Theory]
        [InlineData("http://foo.com/host-not-validated-here/")]
        [InlineData("https://raw.githubusercontent.com/NuGet/NuGetGallery/master/README.md")]
        public void HasReadMe_WhenUrlIsValid_ReturnsTrue(string validUrl)
        {
            var request = new ReadMeRequest
            {
                ReadMeType = "Url",
                ReadMeUrl = validUrl
            };

            Assert.True(ReadMeHelper.HasReadMe(request));
        }

        [Fact]
        public void HasReadMe_WhenFileIsNull_ReturnsFalse()
        {
            var request = new ReadMeRequest
            {
                ReadMeType = "File"
            };

            Assert.False(ReadMeHelper.HasReadMe(request));
        }

        [Fact]
        public void HasReadMe_WhenFileIsEmpty_ReturnsFalse()
        {
            var request = GetReadMeRequest(ReadMeHelper.TypeFile, string.Empty);

            Assert.False(ReadMeHelper.HasReadMe(request));
        }

        [Fact]
        public void HasReadMe_WhenFileIsNotNullOrEmpty_ReturnsTrue()
        {
            var request = GetReadMeRequest(ReadMeHelper.TypeFile, "markdown");

            Assert.True(ReadMeHelper.HasReadMe(request));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void HasReadMe_WhenWrittenIsMissing_ReturnsFalse(string writtenText)
        {
            var request = GetReadMeRequest(ReadMeHelper.TypeWritten, writtenText);

            Assert.False(ReadMeHelper.HasReadMe(request));
        }

        [Fact]
        public void HasReadMe_WhenWrittenIsMissing_ReturnsTrue()
        {
            var request = GetReadMeRequest(ReadMeHelper.TypeWritten, "# ReadMe Example");

            Assert.True(ReadMeHelper.HasReadMe(request));
        }

        [Theory]
        [InlineData(ReadMeHelper.TypeFile)]
        [InlineData(ReadMeHelper.TypeWritten)]
        public async Task GetReadMeMarkdownStream_WhenValid_ReturnsMarkdownData(string readMeType)
        {
            var markdown = "# Hello, World!";
            var request = GetReadMeRequest(readMeType, markdown);

            using (var mdStream = await ReadMeHelper.GetReadMeMarkdownStream(request))
            {
                Assert.Equal(markdown, await mdStream.ReadToEndAsync());
            }
        }

        [Fact]
        public async Task GetReadMeMarkdownStream_WhenInvalidType_Throws()
        {
            var request = GetReadMeRequest("UnknownType", string.Empty);

            await Assert.ThrowsAsync<InvalidOperationException>(() => ReadMeHelper.GetReadMeMarkdownStream(request));
        }

        [Theory]
        [InlineData(ReadMeHelper.TypeFile)]
        [InlineData(ReadMeHelper.TypeWritten)]
        public async Task GetReadMeMarkdownStream_WhenMaxFileSizeReached_ThrowsArgumentException(string readMeType)
        {
            var largeMarkdown = new string('x', ReadMeHelper.MaxFileSize);
            var request = GetReadMeRequest(readMeType, largeMarkdown);

            await Assert.ThrowsAsync<ArgumentException>(() => ReadMeHelper.GetReadMeMarkdownStream(request));
        }

        private ReadMeRequest GetReadMeRequest(string readMeType, string markdown)
        {
            var request = new ReadMeRequest() { ReadMeType = readMeType };

            switch (readMeType)
            {
                case ReadMeHelper.TypeFile:
                    var fileMock = new Mock<HttpPostedFileBase>();
                    fileMock.Setup(f => f.ContentLength).Returns(markdown.Length);
                    fileMock.Setup(f => f.InputStream).Returns(new MemoryStream(Encoding.UTF8.GetBytes(markdown)));
                    request.ReadMeFile = fileMock.Object;
                    break;

                case ReadMeHelper.TypeWritten:
                    request.ReadMeWritten = markdown;
                    break;
            }

            return request;
        }

        [Theory]
        [MemberData(nameof(AntiXssTestScenarios))]
        public async Task GetReadMeHtmlStream_Sanitizes(string test, string input, string expected)
        {
            var actualHtml = await GetHtmlForWrittenMarkdownRequest(input);

            Assert.Equal(expected, actualHtml);
        }

        private async Task<string> GetHtmlForWrittenMarkdownRequest(string writtenText)
        {
            var request = GetReadMeRequest(ReadMeHelper.TypeWritten, writtenText);

            using (var stream = await ReadMeHelper.GetReadMeHtmlStream(request))
            {
                return await stream.ReadToEndAsync();
            }
        }

        /// <summary>
        /// HTML sanitzation test scenarios, taken from the Microsoft Anti-XSS library (GetSafeHtmlFragment).
        /// Differences between Anti-XSS and HtmlSanitizer (ignoring whitespace) are specified inline. Note that
        /// input is treated as markdown, and some differences may be due to the markdown to Html conversion.
        /// </summary>
        /// <see cref="https://wpl.codeplex.com/SourceControl/latest#release/v4.3/Microsoft.Security.Application.HtmlSanitization.Tests/SanitizerTests.cs" />
        public static TheoryData<string, string, string> AntiXssTestScenarios = new TheoryData<string, string, string> {
            {
              "StyleableAttributeOnTagShouldBeRemoved",
              "<p style=\"\"></p>",
              "<p></p>"
            },
            {
              "StyleableAttributeOnTagShouldBeRemovedLeavingOtherTags",
              "<a href=\"\" style=\"\"></a>",
              "<p><a href=\"\"></a></p>"
              // Anti-XSS: no <p>...</p>
            },
            {
              "OnClickAttributeOnTagShouldBeRemoved",
              "<p onclick=\"\"></p>",
              "<p></p>"
            },
            {
              "OnMouseOverAttributeOnTagShouldBeRemoved",
              "<p onmouseover=\"\"></p>",
              "<p></p>"
            },
            {
              "ErrorAttributeShouldBeRemoved",
              "<img src=\"\" onerror=\"XSS\" />",
              "<img src=\"\">"
            },
            {
              "ScriptTagInBodyShouldBeRemoved",
              "<script></script>",
              ""
            },
            {
              "StyleTagShouldBeRemovedUserTwo",
              "<div style=\"font-family:Foo,Bar\\,'a\\a';font-family:';color:expression(alert(1));y'\">aaa</div>",
              "<div>aaa</div>"
            },
            {
              "BreakTagsShouldNotBeRemoved",
              "<br>",
              "<br>"
            },
            {
              "NewLinesShouldNotBeAdded",
              "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed nunc tellus, consectetur eget blandit euismod, pharetra a libero. In pretium, sem sed mollis hendrerit, libero metus condimentum tellus, eget adipiscing odio ligula at velit. Nulla luctus nisl quis sem venenatis ut suscipit mauris posuere.",
              "<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed nunc tellus, consectetur eget blandit euismod, pharetra a libero. In pretium, sem sed mollis hendrerit, libero metus condimentum tellus, eget adipiscing odio ligula at velit. Nulla luctus nisl quis sem venenatis ut suscipit mauris posuere.</p>"
              // Anti-XSS: no <p>...</p>
            },
            {
              "DivTagsShouldNotBeAdded",
              "<input type=\"text\" />",
              "<input type=\"text\">"
            },
            {
              "DuplicateAttributesShouldBeHandledAppropriately",
              "<p id=\"\" id=\"\" style=\"\" style=\"\"></p>",
              "<p></p>"
              // Anti-XSS: <p id="" id=""></p>
            },
            {
              "ShouldNotRemoveNonOffendingText",
              "<script>alert('hi');</script>This text is removed",
              "This text is removed"
            },
            {
              "ChildControlsShouldNotBeRemoved",
              "<a href=\"\" target=\"\"><img src=\"\" /> My Image</a>",
              "<p><a href=\"\" target=\"\"><img src=\"\"> My Image</a></p>"
              // Anti-XSS: no <p>...</p>
            },
            {
              "NonBlacklistedTagsShouldNotBeRemoved",
              "<b>Some text</b><strong>More text</strong>",
              "<p><b>Some text</b><strong>More text</strong></p>"
              // Anti-XSS: no <p>...</p>
            },
            {
              "ScriptInImageSourceShouldBeRemoved",
              "<img src=\"javascript:alert('XSS');\">",
              "<img>"
              // Anti-XSS: <img src="">
            },
            {
              "ScriptInImageSourceShouldBeRemovedTwo",
              "<img src=javascript:alert('XSS');>",
              "<p>&lt;img src=javascript:alert('XSS');&gt;</p>"
              // Anti-XSS: <img src="">
            },
            {
              "ScriptInImageSourceShouldBeRemovedThree",
              "<img src=jav   ascript:alert('XSS');>",
              "<p>&lt;img src=jav   ascript:alert('XSS');&gt;</p>"
              // Anti-XSS: <img src="jav">
            },
            {
              "ScriptInMalformedImageTagShouldBeRemoved",
              "<img><script>alert(\"XSS\")</script></img>",
              "<p><img></p>"
              // Anti-XSS: no <p>...</p>
            },
            {
              "SourceEncodingShouldBeRemovedFromImageTag",
              "<IMG SRC=\"jav&#x09;ascript:alert('XSS');\">",
              "<img>"
              // Anti-XSS: <img src="">
            },
            {
              "TitleTagsShouldBeRemoved",
              "<title></title>",
              ""
            },
            {
              "LinkTagsShouldBeRemoved",
              "<link rel=javascript:alert('XSS');>",
              "<p>&lt;link rel=javascript:alert('XSS');&gt;</p>"
              // Anti-XSS: (empty)
            },
            {
              "MetaTagsShouldBeRemoved",
              "<meta http-equiv=\"refresh\" content=\"0;url=javascript:alert('XSS');\">",
              ""
            },
            {
              "ScriptInTableBackgroundAttributeShouldBeRemoved",
              "<table background=\"javascript:alert('XSS');\"></table>",
              "<table></table>"
              // Anti-XSS: <table background=""></table>
            },
            {
              "ObjectTagsShouldBeRemoved",
              "<object classid=clsid:ae24fdae-03c6-11d1-8b76-0080c744f389><param name=url value=javascript:alert('XSS')></object>",
              "<p></p>"
              // Anti-XSS: no <p>...</p>
            },
            {
              "EmbedTagsShouldBeRemoved",
              "<embed src=\"\" AllowScriptAccess=\"always\"></embed>",
              "<p></p>"
              // Anti-XSS: no <p>...</p>
            },
            {
              "XMLTagsShouldBeRemoved",
              "<xml id=\"xss\"></xml>",
              "<p></p>"
              // Anti-XSS: no <p>...</p>
            },
            {
              "OutOfOrderTagsShouldStillRemoveScripts",
              "<div><p></div><p><script src=\"\" /></p>",
              "<div><p></p></div><p></p>"
            },
            {
              "OutOfOrderTagsShouldStillRemoveScriptsTwo",
              "<div><p><div><p><script src=\"\" /></p>",
              "<div><p></p><div><p></p></div></div>"
            }
        };
    }
}

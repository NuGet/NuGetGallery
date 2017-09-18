﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class ReadMeServiceFacts
    {
        public class TheHasReadMeSourceMethod
        {
            internal ReadMeService ReadMeService = new ReadMeService(new Mock<IPackageFileService>().Object);

            [Fact]
            public void WhenRequestIsNull_ReturnsFalse()
            {
                Assert.False(ReadMeService.HasReadMeSource(null));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("InvalidType")]
            public void WhenTypeIsUnknown_ReturnsFalse(string sourceType)
            {
                Assert.False(ReadMeService.HasReadMeSource(new ReadMeRequest() { SourceType = sourceType }));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public void WhenWrittenAndSourceTextMissing_ReturnsFalse(string sourceText)
            {
                Assert.False(ReadMeService.HasReadMeSource(new ReadMeRequest() { SourceType = ReadMeService.TypeWritten, SourceText = sourceText }));
            }

            [Fact]
            public void WhenWrittenAndHasSourceText_ReturnsTrue()
            {
                Assert.True(ReadMeService.HasReadMeSource(new ReadMeRequest() { SourceType = ReadMeService.TypeWritten, SourceText = "markdown" }));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public void WhenUrlAndSourceUrlMissing_ReturnsFalse(string sourceUrl)
            {
                Assert.False(ReadMeService.HasReadMeSource(new ReadMeRequest() { SourceType = ReadMeService.TypeUrl, SourceUrl = sourceUrl }));
            }

            [Fact]
            public void WhenUrlAndHasSourceUrl_ReturnsTrue()
            {
                Assert.True(ReadMeService.HasReadMeSource(new ReadMeRequest() { SourceType = ReadMeService.TypeUrl, SourceUrl = "sourceUrl" }));
            }

            [Fact]
            public void WhenFileAndSourceFileMissing_ReturnsFalse()
            {
                Assert.False(ReadMeService.HasReadMeSource(new ReadMeRequest() { SourceType = ReadMeService.TypeFile }));
            }

            [Fact]
            public void WhenFileAndSourceFileNotEmpty_ReturnsTrue()
            {
                // Arrange.
                var sourceFile = new Mock<HttpPostedFileBase>();
                sourceFile.Setup(f => f.ContentLength).Returns(10);

                // Act & Assert.
                Assert.True(ReadMeService.HasReadMeSource(new ReadMeRequest() { SourceType = ReadMeService.TypeFile, SourceFile = sourceFile.Object }));
            }
        }
        
        public class TheGetReadMeHtmlMethod
        {
            [Theory]
            [InlineData("<script>alert('test')</script>", "<p>&lt;script&gt;alert('test')&lt;/script&gt;</p>")]
            [InlineData("<img src=\"javascript:alert('test');\">", "<p>&lt;img src=&quot;javascript:alert('test');&quot;&gt;</p>")]
            [InlineData("<a href=\"javascript:alert('test');\">", "<p>&lt;a href=&quot;javascript:alert('test');&quot;&gt;</p>")]
            public void EncodesHtmlInMarkdown(string originalMd, string expectedHtml)
            {
                Assert.Equal(expectedHtml, StripNewLines(ReadMeService.GetReadMeHtml(originalMd)));
            }

            [Theory]
            [InlineData("# Heading", "<h1>Heading</h1>")]
            [InlineData("- List", "<ul><li>List</li></ul>")]
            [InlineData("[text](http://www.test.com)", "<p><a href=\"http://www.test.com\">text</a></p>")]
            public void ConvertsMarkdownToHtml(string originalMd, string expectedHtml)
            {
                Assert.Equal(expectedHtml, StripNewLines(ReadMeService.GetReadMeHtml(originalMd)));
            }

            private static string StripNewLines(string text)
            {
                return text.Replace("\r\n", "").Replace("\n", "");
            }
        }

        public class TheGetReadMeMdAsyncMethod
        {
            private readonly string LargeMarkdown = new string('x', ReadMeService.MaxMdLengthBytes + 1);

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            [InlineData("invalid")]
            public async Task WhenInvalidSourceType_ThrowsInvalidOperationException(string sourceType)
            {
                // Arrange.
                var request = ReadMeServiceFacts.GetReadMeRequest(sourceType, "markdown");

                // Act & Assert.
                await Assert.ThrowsAsync<InvalidOperationException>(() => ReadMeService.GetReadMeMdAsync(request, Encoding.UTF8));
            }

            [Theory]
            [InlineData(ReadMeService.TypeWritten)]
            [InlineData(ReadMeService.TypeFile)]
            public async Task WhenMaxLengthExceeded_ThrowsInvalidOperationException(string sourceType)
            {
                // Arrange.
                var request = ReadMeServiceFacts.GetReadMeRequest(ReadMeService.TypeWritten, LargeMarkdown);

                // Act & Assert.
                await Assert.ThrowsAsync<InvalidOperationException>(() => ReadMeService.GetReadMeMdAsync(request, Encoding.UTF8));
            }

            [Theory]
            [InlineData(ReadMeService.TypeWritten)]
            [InlineData(ReadMeService.TypeFile)]
            public async Task WhenValid_ReturnsSourceContent(string sourceType)
            {
                // Arrange.
                var request = ReadMeServiceFacts.GetReadMeRequest(sourceType, "markdown");

                // Act & Assert.
                Assert.Equal("markdown", await ReadMeService.GetReadMeMdAsync(request, Encoding.UTF8));
            }

            [Theory]
            [InlineData("exe")]
            [InlineData("txt")]
            [InlineData("zip")]
            public async Task WhenFileAndExtensionInvalid_ThrowsInvalidOperationException(string fileExt)
            {
                // Arrange.
                var request = ReadMeServiceFacts.GetReadMeRequest(ReadMeService.TypeFile, "markdown", fileName: $"README.{fileExt}");

                // Act & Assert.
                await Assert.ThrowsAsync<InvalidOperationException>(() => ReadMeService.GetReadMeMdAsync(request, Encoding.UTF8));
            }

            [Theory]
            [InlineData("invalid")]
            [InlineData("www.github.com")]
            public async Task WhenInvalidUrl_ThrowsInvalidOperationException(string url)
            {
                // Arrange.
                var request = ReadMeServiceFacts.GetReadMeRequest(ReadMeService.TypeUrl, "markdown");

                // Act & Assert.
                await Assert.ThrowsAsync<ArgumentException>(() => ReadMeService.GetReadMeMdAsync(request, Encoding.UTF8));
            }
        }

        private static ReadMeRequest GetReadMeRequest(string sourceType, string markdown, string fileName = "README.md", string url = "")
        {
            var request = new ReadMeRequest() { SourceType = sourceType };

            switch (sourceType)
            {
                case ReadMeService.TypeFile:
                    var fileMock = new Mock<HttpPostedFileBase>();
                    fileMock.Setup(f => f.FileName).Returns(fileName);
                    fileMock.Setup(f => f.ContentLength).Returns(markdown.Length);
                    fileMock.Setup(f => f.InputStream).Returns(new MemoryStream(Encoding.UTF8.GetBytes(markdown)));
                    request.SourceFile = fileMock.Object;
                    break;

                case ReadMeService.TypeWritten:
                    request.SourceText = markdown;
                    break;

                case ReadMeService.TypeUrl:
                    request.SourceUrl = url;
                    break;
            }

            return request;
        }
    }
}

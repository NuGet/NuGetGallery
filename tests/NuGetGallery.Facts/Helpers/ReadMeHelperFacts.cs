// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Moq;
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
            Assert.False(ReadMeHelper.HasReadMe(new ReadMeRequest() { ReadMeSourceType = readMeType }));
        }
        
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("github.com")]
        public void HasReadMe_WhenUrlIsInvalid_ReturnsFalse(string invalidUrl)
        {
            var request = new ReadMeRequest
            {
                ReadMeSourceType = ReadMeHelper.TypeUrl,
                SourceUrl = invalidUrl
            };

            Assert.False(ReadMeHelper.HasReadMe(request));
        }

        [Theory]
        [InlineData("http://unit.test/host-not-validated-here/")]
        [InlineData("https://raw.githubusercontent.com/NuGet/NuGetGallery/master/README.md")]
        public void HasReadMe_WhenUrlIsValid_ReturnsTrue(string validUrl)
        {
            var request = new ReadMeRequest
            {
                ReadMeSourceType = ReadMeHelper.TypeUrl,
                SourceUrl = validUrl
            };

            Assert.True(ReadMeHelper.HasReadMe(request));
        }

        [Fact]
        public void HasReadMe_WhenFileIsNull_ReturnsFalse()
        {
            var request = new ReadMeRequest
            {
                ReadMeSourceType = ReadMeHelper.TypeFile
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
            var largeMarkdown = new string('x', ReadMeHelper.MaxReadMeLengthBytes);
            var request = GetReadMeRequest(readMeType, largeMarkdown);

            await Assert.ThrowsAsync<InvalidOperationException>(() => ReadMeHelper.GetReadMeMarkdownStream(request));
        }

        private ReadMeRequest GetReadMeRequest(string readMeType, string markdown)
        {
            var request = new ReadMeRequest() { ReadMeSourceType = readMeType };

            switch (readMeType)
            {
                case ReadMeHelper.TypeFile:
                    var fileMock = new Mock<HttpPostedFileBase>();
                    fileMock.Setup(f => f.FileName).Returns("README.md");
                    fileMock.Setup(f => f.ContentLength).Returns(markdown.Length);
                    fileMock.Setup(f => f.InputStream).Returns(new MemoryStream(Encoding.UTF8.GetBytes(markdown)));
                    request.SourceFile = fileMock.Object;
                    break;

                case ReadMeHelper.TypeWritten:
                    request.SourceText = markdown;
                    break;
            }

            return request;
        }
    }
}

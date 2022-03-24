﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class ReadMeServiceFacts
    {
        public class TheSaveReadMeMdIfChangedMethod
        {
            private readonly Package _package;
            private readonly EditPackageVersionReadMeRequest _edit;
            private readonly Encoding _encoding;
            private readonly Mock<IPackageFileService> _packageFileService;
            private readonly Mock<IEntitiesContext> _entitiesContext;
            private readonly Mock<IMarkdownService> _markdownService;
            private readonly Mock<ICoreReadmeFileService> _coreReadmeFileService;
            private readonly ReadMeService _target;

            public TheSaveReadMeMdIfChangedMethod()
            {
                _package = new Package
                {
                    HasReadMe = false,
                };
                _edit = new EditPackageVersionReadMeRequest
                {
                    ReadMe = new ReadMeRequest
                    {
                        SourceText = "# Title" + Environment.NewLine + "Some *groovy* content.",
                        SourceType = "written",
                    },
                    ReadMeState = PackageEditReadMeState.Changed,
                };
                _encoding = Encoding.UTF8;

                _packageFileService = new Mock<IPackageFileService>();
                _entitiesContext = new Mock<IEntitiesContext>();
                _markdownService = new Mock<IMarkdownService>();
                _coreReadmeFileService = new Mock<ICoreReadmeFileService>();

                _target = new ReadMeService(
                    _packageFileService.Object,
                    _entitiesContext.Object,
                    _markdownService.Object,
                    _coreReadmeFileService.Object);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task OnlyCommitsWhenSpecifiedAndUpdatingReadme(bool commitChanges)
            {
                // Arrange
                _edit.ReadMeState = PackageEditReadMeState.Unchanged;

                // Act
                var changed = await _target.SaveReadMeMdIfChanged(
                    _package,
                    _edit,
                    _encoding,
                    commitChanges);

                // Assert
                Assert.True(changed);
                Assert.True(_package.HasReadMe);
                Assert.Equal(PackageEditReadMeState.Changed, _edit.ReadMeState);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    commitChanges ? Times.Once() : Times.Never());
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task NeverCommitsWhenReadmeExistsAndHasNotChanged(bool commitChanges)
            {
                // Arrange
                _packageFileService
                    .Setup(x => x.DownloadReadMeMdFileAsync(_package))
                    .ReturnsAsync(_edit.ReadMe.SourceText);

                _package.HasReadMe = true;
                _edit.ReadMeState = PackageEditReadMeState.Changed;

                // Act
                var changed = await _target.SaveReadMeMdIfChanged(
                    _package,
                    _edit,
                    _encoding,
                    commitChanges);

                // Assert
                Assert.False(changed);
                Assert.True(_package.HasReadMe);
                Assert.Equal(PackageEditReadMeState.Unchanged, _edit.ReadMeState);
                _entitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task NeverCommitsWhenReadmeStillDoesNotExist(bool commitChanges)
            {
                // Arrange
                _package.HasReadMe = false;
                _edit.ReadMe.SourceText = null;
                _edit.ReadMeState = PackageEditReadMeState.Changed;

                // Act
                var changed = await _target.SaveReadMeMdIfChanged(
                    _package,
                    _edit,
                    _encoding,
                    commitChanges);

                // Assert
                Assert.False(changed);
                Assert.False(_package.HasReadMe);
                Assert.Equal(PackageEditReadMeState.Unchanged, _edit.ReadMeState);
                _entitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task OnlyCommitsWhenSpecifiedAndRemovingReadme(bool commitChanges)
            {
                // Arrange
                _packageFileService
                    .Setup(x => x.DownloadReadMeMdFileAsync(_package))
                    .ReturnsAsync(_edit.ReadMe.SourceText);

                _package.HasReadMe = true;
                _edit.ReadMe.SourceText = null;
                _edit.ReadMeState = PackageEditReadMeState.Unchanged;

                // Act
                var changed = await _target.SaveReadMeMdIfChanged(
                    _package,
                    _edit,
                    _encoding,
                    commitChanges);

                // Assert
                Assert.True(changed);
                Assert.False(_package.HasReadMe);
                Assert.Equal(PackageEditReadMeState.Deleted, _edit.ReadMeState);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    commitChanges ? Times.Once() : Times.Never());
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task ThrowsArgumentExceptionWhenReadmeUrlHostInvalid(bool commitChanges)
            {
                // Arrange
                _package.HasReadMe = true;
                _packageFileService.Setup(m => m.DownloadReadMeMdFileAsync(_package)).ReturnsAsync((string)null);
                _edit.ReadMe = new ReadMeRequest { SourceUrl = "https://github.com/username/markdown-here/blob/master/README.md", SourceType = "url" };
                _edit.ReadMeState = PackageEditReadMeState.Changed;

                // Act
                var saveTask = _target.SaveReadMeMdIfChanged(
                    _package,
                    _edit,
                    _encoding,
                    commitChanges);

                // Assert
                var exception = await Assert.ThrowsAsync<ArgumentException>(() => saveTask);

                Assert.Contains(Strings.ReadMeUrlHostInvalid, exception.Message);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never());
            }
        }

        public class TheHasReadMeSourceMethod
        {
            internal ReadMeService ReadMeService = new ReadMeService(
                new Mock<IPackageFileService>().Object,
                new Mock<IEntitiesContext>().Object,
                new Mock<IMarkdownService>().Object,
                new Mock<ICoreReadmeFileService>().Object);

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
        
        public class TheGetReadMeHtmlAsyncMethod
        {
            [Fact]
            public async Task WhenReadMeDoesNotExistReturnsNull()
            {
                // Arrange
                var readMeService = new ReadMeService(
                    new Mock<IPackageFileService>().Object, 
                    new Mock<IEntitiesContext>().Object,
                    new Mock<IMarkdownService>().Object,
                    new Mock<ICoreReadmeFileService>().Object);
                var package = new Package() { HasReadMe = false };

                // Act & Assert
                Assert.Null((await readMeService.GetReadMeHtmlAsync(package)).Content);
            }
        }

        public class TheGetReadMeMdAsyncMethod
        {
            private readonly string LargeMarkdown = new string('x', ReadMeService.MaxAllowedReadmeBytes + 1);

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
                var request = ReadMeServiceFacts.GetReadMeRequest(sourceType, LargeMarkdown);

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
                var request = ReadMeServiceFacts.GetReadMeRequest(ReadMeService.TypeUrl, "markdown", url: url);

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

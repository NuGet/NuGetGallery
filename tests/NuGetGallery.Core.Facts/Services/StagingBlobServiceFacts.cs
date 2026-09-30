// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class StagingBlobServiceFacts
    {
        [Fact]
        public async Task SavesPackageToImmutablePath()
        {
            var content = new byte[] { 1, 2, 3 };
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.GetETagOrNullAsync(
                    CoreConstants.Folders.StagingFolderName,
                    It.IsAny<string>()))
                .ReturnsAsync("\"etag\"");

            var result = await new StagingBlobService(storage.Object).SavePackageFileAsync(
                "NuGet.Versioning",
                "3.4.0",
                new MemoryStream(content));

            Assert.StartsWith("nuget.versioning/3.4.0/", result.Path);
            Assert.EndsWith(".nupkg", result.Path);
            Assert.Equal("\"etag\"", result.ETag);
            storage.Verify(x => x.SaveFileAsync(
                CoreConstants.Folders.StagingFolderName,
                result.Path,
                CoreConstants.PackageContentType,
                It.IsAny<Stream>(),
                false));
        }

        [Fact]
        public async Task SavesSymbolPackageToPrivateSnupkgPath()
        {
            var storage = new Mock<ICoreFileStorageService>();
            storage.Setup(x => x.GetETagOrNullAsync(CoreConstants.Folders.StagingFolderName, It.IsAny<string>()))
                .ReturnsAsync("\"symbol-etag\"");

            var result = await new StagingBlobService(storage.Object).SaveSymbolPackageFileAsync(
                "NuGet.Versioning", "3.4.0", new MemoryStream(new byte[] { 1, 2, 3 }));

            Assert.StartsWith("nuget.versioning/3.4.0/", result.Path);
            Assert.EndsWith(".snupkg", result.Path);
            Assert.Equal("\"symbol-etag\"", result.ETag);
            storage.Verify(x => x.SaveFileAsync(
                CoreConstants.Folders.StagingFolderName, result.Path,
                CoreConstants.PackageContentType, It.IsAny<Stream>(), false), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetsReadUriWhenUploadedETagMatches(bool explicitExpiration)
        {
            var expected = new Uri("https://example.test/staged-package");
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.GetETagOrNullAsync(CoreConstants.Folders.StagingFolderName, "package/path"))
                .ReturnsAsync("\"etag\"");
            storage
                .Setup(x => x.GetFileReadUriAsync(
                    CoreConstants.Folders.StagingFolderName,
                    "package/path",
                    It.IsAny<DateTimeOffset?>()))
                .ReturnsAsync(expected);
            var target = new StagingBlobService(storage.Object);
            var before = DateTimeOffset.UtcNow;
            var requestedExpiration = before.AddDays(5);

            Uri actual;
            if (explicitExpiration)
            {
                actual = await target.GetPackageReadUriAsync("package/path", "\"etag\"", requestedExpiration);
            }
            else
            {
                actual = await target.GetPackageReadUriAsync("package/path", "\"etag\"");
            }

            Assert.Same(expected, actual);
            if (explicitExpiration)
            {
                storage.Verify(x => x.GetFileReadUriAsync(CoreConstants.Folders.StagingFolderName, "package/path", requestedExpiration), Times.Once);
            }
            else
            {
                var latestExpiration = DateTimeOffset.UtcNow.AddMinutes(10);
                storage.Verify(x => x.GetFileReadUriAsync(
                    CoreConstants.Folders.StagingFolderName,
                    "package/path",
                    It.Is<DateTimeOffset?>(expiry => expiry >= before.AddMinutes(10) && expiry <= latestExpiration)), Times.Once);
            }
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("\"different-etag\"", false)]
        [InlineData(null, true)]
        [InlineData("\"different-etag\"", true)]
        public async Task RejectsReadWhenUploadedETagDoesNotMatch(string currentETag, bool explicitExpiration)
        {
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.GetETagOrNullAsync(CoreConstants.Folders.StagingFolderName, "package/path"))
                .ReturnsAsync(currentETag);
            var target = new StagingBlobService(storage.Object);

            if (explicitExpiration)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => target.GetPackageReadUriAsync("package/path", "\"expected-etag\"", DateTimeOffset.UtcNow.AddDays(5)));
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => target.GetPackageReadUriAsync("package/path", "\"expected-etag\""));
            }

            storage.Verify(
                x => x.GetFileReadUriAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset?>()),
                Times.Never);
        }

        [Fact]
        public async Task OpensPackageWhenETagMatches()
        {
            var expected = new MemoryStream();
            var file = new Mock<IFileReference>();
            file.SetupGet(x => x.ContentId).Returns("\"etag\"");
            file.Setup(x => x.OpenRead()).Returns(expected);
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.GetFileReferenceAsync(CoreConstants.Folders.StagingFolderName, "package/path", null))
                .ReturnsAsync(file.Object);
            var target = new StagingBlobService(storage.Object);

            var actual = await target.OpenPackageFileAsync("package/path", "\"etag\"");

            Assert.Same(expected, actual);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("\"different-etag\"")]
        public async Task RejectsOpenWhenETagDoesNotMatch(string currentETag)
        {
            var file = currentETag == null ? null : Mock.Of<IFileReference>(x => x.ContentId == currentETag);
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.GetFileReferenceAsync(CoreConstants.Folders.StagingFolderName, "package/path", null))
                .ReturnsAsync(file);
            var target = new StagingBlobService(storage.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.OpenPackageFileAsync("package/path", "\"expected-etag\""));
        }
    }
}

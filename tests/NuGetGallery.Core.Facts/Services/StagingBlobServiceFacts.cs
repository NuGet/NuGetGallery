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
        public async Task GetsReadUriWhenUploadedETagMatches()
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

            var actual = await target.GetPackageReadUriAsync("package/path", "\"etag\"");

            Assert.Same(expected, actual);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("\"different-etag\"")]
        public async Task RejectsReadWhenUploadedETagDoesNotMatch(string currentETag)
        {
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.GetETagOrNullAsync(CoreConstants.Folders.StagingFolderName, "package/path"))
                .ReturnsAsync(currentETag);
            var target = new StagingBlobService(storage.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.GetPackageReadUriAsync("package/path", "\"expected-etag\""));

            storage.Verify(
                x => x.GetFileReadUriAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset?>()),
                Times.Never);
        }
    }
}

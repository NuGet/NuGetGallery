// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
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
                .Setup(x => x.GetFileReferenceAsync(
                    CoreConstants.Folders.StagingFolderName,
                    It.IsAny<string>(),
                    null))
                .ReturnsAsync(Mock.Of<IFileReference>(reference => reference.ContentId == "\"etag\""));

            var result = await new StagingBlobService(storage.Object).SavePackageFileAsync(
                "NuGet.Versioning",
                "3.4.0",
                new MemoryStream(content));

            Assert.StartsWith("nuget.versioning/3.4.0/", result.Path);
            Assert.EndsWith(".nupkg", result.Path);
            Assert.Equal("\"etag\"", result.ETag);
            Assert.Equal(content.Length, result.Length);

            using (var algorithm = SHA512.Create())
            {
                Assert.Equal(
                    Convert.ToBase64String(algorithm.ComputeHash(content)),
                    result.ContentHash);
            }

            storage.Verify(x => x.SaveFileAsync(
                CoreConstants.Folders.StagingFolderName,
                result.Path,
                CoreConstants.PackageContentType,
                It.IsAny<Stream>(),
                false));
        }

        [Fact]
        public async Task CopiesStagedPackageToValidationSetWithSourceETag()
        {
            var sourceUri = new Uri("https://staging.test/staging/package/1.0.0/source.nupkg?sig=token");
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.GetFileReadUriAsync(
                    CoreConstants.Folders.StagingFolderName,
                    "package/1.0.0/source.nupkg",
                    It.IsAny<DateTimeOffset?>()))
                .ReturnsAsync(sourceUri);

            var sourceBlob = new Mock<ISimpleCloudBlob>();
            var copyState = new Mock<ICloudBlobCopyState>();
            copyState.SetupGet(x => x.Status).Returns(CloudBlobCopyStatus.Success);
            var destinationBlob = new Mock<ISimpleCloudBlob>();
            destinationBlob.SetupGet(x => x.CopyState).Returns(copyState.Object);
            var destinationContainer = new Mock<ICloudBlobContainer>();
            destinationContainer
                .Setup(x => x.GetBlobReference("validation-sets/tracking/package.1.0.0.nupkg"))
                .Returns(destinationBlob.Object);
            var validationStorageClient = new Mock<ICloudBlobClient>();
            validationStorageClient.Setup(x => x.GetBlobFromUri(sourceUri)).Returns(sourceBlob.Object);
            validationStorageClient
                .Setup(x => x.GetContainerReference(CoreConstants.Folders.ValidationFolderName))
                .Returns(destinationContainer.Object);

            var service = new StagingBlobService(storage.Object);

            await service.CopyStagedPackageToValidationSetAsync(
                "package/1.0.0/source.nupkg",
                "\"source-etag\"",
                validationStorageClient.Object,
                "validation-sets/tracking/package.1.0.0.nupkg");

            destinationBlob.Verify(x => x.StartCopyAsync(
                sourceBlob.Object,
                It.Is<IAccessCondition>(condition =>
                    condition.IfMatchETag == "\"source-etag\"" &&
                    condition.IfNoneMatchETag == null),
                It.Is<IAccessCondition>(condition =>
                    condition.IfMatchETag == null &&
                    condition.IfNoneMatchETag == "*")),
                Times.Once);
        }

        [Fact]
        public async Task ReusesSuccessfulValidationSetCopy()
        {
            var storage = new Mock<ICoreFileStorageService>();
            var copyState = new Mock<ICloudBlobCopyState>();
            copyState.SetupGet(x => x.Status).Returns(CloudBlobCopyStatus.Success);
            var destinationBlob = new Mock<ISimpleCloudBlob>();
            destinationBlob.Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            destinationBlob.SetupGet(x => x.CopyState).Returns(copyState.Object);
            var destinationContainer = new Mock<ICloudBlobContainer>();
            destinationContainer
                .Setup(x => x.GetBlobReference("validation-sets/tracking/package.1.0.0.nupkg"))
                .Returns(destinationBlob.Object);
            var validationStorageClient = new Mock<ICloudBlobClient>();
            validationStorageClient
                .Setup(x => x.GetContainerReference(CoreConstants.Folders.ValidationFolderName))
                .Returns(destinationContainer.Object);

            var service = new StagingBlobService(storage.Object);

            await service.CopyStagedPackageToValidationSetAsync(
                "package/1.0.0/source.nupkg",
                "\"source-etag\"",
                validationStorageClient.Object,
                "validation-sets/tracking/package.1.0.0.nupkg");

            destinationBlob.Verify(x => x.FetchAttributesAsync(), Times.Once);
            destinationBlob.Verify(
                x => x.StartCopyAsync(
                    It.IsAny<ISimpleCloudBlob>(),
                    It.IsAny<IAccessCondition>(),
                    It.IsAny<IAccessCondition>()),
                Times.Never);
            storage.Verify(
                x => x.GetFileReadUriAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset?>()),
                Times.Never);
        }

        [Theory]
        [InlineData(CloudBlobCopyStatus.Failed)]
        [InlineData(CloudBlobCopyStatus.Aborted)]
        public async Task RestartsTerminalValidationSetCopyWithDestinationETag(CloudBlobCopyStatus initialStatus)
        {
            var sourceUri = new Uri("https://staging.test/staging/package/1.0.0/source.nupkg?sig=token");
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.GetFileReadUriAsync(
                    CoreConstants.Folders.StagingFolderName,
                    "package/1.0.0/source.nupkg",
                    It.IsAny<DateTimeOffset?>()))
                .ReturnsAsync(sourceUri);

            var sourceBlob = new Mock<ISimpleCloudBlob>();
            var copyStatus = initialStatus;
            var copyState = new Mock<ICloudBlobCopyState>();
            copyState.SetupGet(x => x.Status).Returns(() => copyStatus);
            var destinationBlob = new Mock<ISimpleCloudBlob>();
            destinationBlob.Setup(x => x.ExistsAsync()).ReturnsAsync(true);
            destinationBlob.SetupGet(x => x.CopyState).Returns(copyState.Object);
            destinationBlob.SetupGet(x => x.ETag).Returns("\"failed-copy-etag\"");
            destinationBlob
                .Setup(x => x.StartCopyAsync(
                    It.IsAny<ISimpleCloudBlob>(),
                    It.IsAny<IAccessCondition>(),
                    It.IsAny<IAccessCondition>()))
                .Callback(() => copyStatus = CloudBlobCopyStatus.Success)
                .Returns(Task.CompletedTask);
            var destinationContainer = new Mock<ICloudBlobContainer>();
            destinationContainer
                .Setup(x => x.GetBlobReference("validation-sets/tracking/package.1.0.0.nupkg"))
                .Returns(destinationBlob.Object);
            var validationStorageClient = new Mock<ICloudBlobClient>();
            validationStorageClient.Setup(x => x.GetBlobFromUri(sourceUri)).Returns(sourceBlob.Object);
            validationStorageClient
                .Setup(x => x.GetContainerReference(CoreConstants.Folders.ValidationFolderName))
                .Returns(destinationContainer.Object);

            var service = new StagingBlobService(storage.Object);

            await service.CopyStagedPackageToValidationSetAsync(
                "package/1.0.0/source.nupkg",
                "\"source-etag\"",
                validationStorageClient.Object,
                "validation-sets/tracking/package.1.0.0.nupkg");

            destinationBlob.Verify(x => x.StartCopyAsync(
                sourceBlob.Object,
                It.Is<IAccessCondition>(condition => condition.IfMatchETag == "\"source-etag\""),
                It.Is<IAccessCondition>(condition => condition.IfMatchETag == "\"failed-copy-etag\"")),
                Times.Once);
        }
    }
}

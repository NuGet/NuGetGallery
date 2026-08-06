// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class StagingBlobServiceFacts
    {
        private const string BlobPath = "v1/2026/08/05/0123456789abcdef0123456789abcdef.nupkg";
        private const string ETag = "\"etag\"";

        [Fact]
        public async Task CreateUsesOpaquePathAndImmutableMetadata()
        {
            var content = new byte[] { 1, 2, 3 };
            var expectedHash = GetHash(content);
            var context = new TestContext(content.Length, ETag);
            IAccessCondition uploadCondition = null;
            context.Blob
                .Setup(x => x.UploadFromStreamAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<IAccessCondition>()))
                .Callback<Stream, IAccessCondition>((_, condition) => uploadCondition = condition)
                .Returns(Task.CompletedTask);
            var target = context.CreateService(initializeContainer: true);

            var result = await target.CreateAsync(
                new MemoryStream(content),
                StagingBlobType.Nupkg);

            Assert.Matches(
                new Regex(@"\Av1/\d{4}/\d{2}/\d{2}/[0-9a-f]{32}\.nupkg\z"),
                result.BlobPath);
            Assert.Equal(ETag, result.ETag);
            Assert.Equal(expectedHash, result.ContentHash);
            Assert.Equal(content.Length, result.ContentLength);
            Assert.Equal(StagingBlobType.Nupkg, result.BlobType);
            Assert.Equal("*", uploadCondition.IfNoneMatchETag);
            Assert.Equal(CoreConstants.PackageContentType, context.Properties.Object.ContentType);
            Assert.Null(context.Properties.Object.CacheControl);
            Assert.Equal(expectedHash, context.Metadata[CoreConstants.Sha512HashAlgorithmId]);
            Assert.Equal("nupkg", context.Metadata[StagingBlobService.ArtifactTypeMetadataKey]);
            Assert.Equal(StagingBlobService.FormatVersion, context.Metadata[StagingBlobService.FormatVersionMetadataKey]);
            context.Container.Verify(x => x.CreateIfNotExistAsync(enablePublicAccess: false), Times.Once);
        }

        [Fact]
        public async Task CreateRejectsDuplicate()
        {
            var context = new TestContext(contentLength: 1, ETag);
            context.Blob
                .Setup(x => x.UploadFromStreamAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<IAccessCondition>()))
                .ThrowsAsync(new CloudBlobPreconditionFailedException(null));

            await Assert.ThrowsAsync<FileAlreadyExistsException>(
                () => context.CreateService().CreateAsync(
                    new MemoryStream(new byte[] { 1 }),
                    StagingBlobType.Nupkg));
        }

        [Fact]
        public async Task CreateRejectsNonSeekableStream()
        {
            var context = new TestContext(contentLength: 0, ETag);
            var stream = new Mock<Stream>();
            stream.SetupGet(x => x.CanRead).Returns(true);
            stream.SetupGet(x => x.CanSeek).Returns(false);

            await Assert.ThrowsAsync<ArgumentException>(
                () => context.CreateService().CreateAsync(
                    stream.Object,
                    StagingBlobType.Snupkg));
        }

        [Fact]
        public async Task OpenReadUsesExpectedETag()
        {
            var reference = CreateReference();
            var context = new TestContext(reference.ContentLength, reference.ETag);
            context.SetExpectedMetadata(reference);
            var expectedStream = new MemoryStream();
            IAccessCondition readCondition = null;
            context.Blob
                .Setup(x => x.OpenReadAsync(It.IsAny<IAccessCondition>()))
                .Callback<IAccessCondition>(condition => readCondition = condition)
                .ReturnsAsync(expectedStream);

            var result = await context.CreateService().OpenReadAsync(reference);

            Assert.Same(expectedStream, result);
            Assert.Equal(reference.ETag, readCondition.IfMatchETag);
        }

        [Fact]
        public async Task OpenReadRejectsETagMismatch()
        {
            var reference = CreateReference();
            var context = new TestContext(reference.ContentLength, "\"different\"");
            context.SetExpectedMetadata(reference);

            await Assert.ThrowsAsync<StagingBlobIntegrityException>(
                () => context.CreateService().OpenReadAsync(reference));

            context.Blob.Verify(
                x => x.OpenReadAsync(It.IsAny<IAccessCondition>()),
                Times.Never);
        }

        [Fact]
        public async Task OpenReadRejectsMissingBlob()
        {
            var reference = CreateReference();
            var context = new TestContext(reference.ContentLength, reference.ETag);
            context.Blob
                .Setup(x => x.FetchAttributesAsync())
                .ThrowsAsync(new CloudBlobNotFoundException(null));

            await Assert.ThrowsAsync<CloudBlobNotFoundException>(
                () => context.CreateService().OpenReadAsync(reference));
        }

        [Fact]
        public async Task CopyUsesSourceAndDestinationConditions()
        {
            var reference = CreateReference();
            var context = new TestContext(reference.ContentLength, reference.ETag);
            context.SetExpectedMetadata(reference);
            var destination = context.CreateDestination(reference);
            var destinationCondition = AccessConditionWrapper.GenerateIfMatchCondition("\"destination\"");
            IAccessCondition sourceCondition = null;
            IAccessCondition actualDestinationCondition = null;
            destination
                .Setup(x => x.StartCopyAsync(
                    context.Blob.Object,
                    It.IsAny<IAccessCondition>(),
                    It.IsAny<IAccessCondition>()))
                .Callback<ISimpleCloudBlob, IAccessCondition, IAccessCondition>(
                    (_, source, target) =>
                    {
                        sourceCondition = source;
                        actualDestinationCondition = target;
                    })
                .Returns(Task.CompletedTask);

            await context.CreateService().CopyAsync(
                reference,
                CoreConstants.Folders.ValidationFolderName,
                "validation-sets/package.nupkg",
                destinationCondition);

            Assert.Equal(reference.ETag, sourceCondition.IfMatchETag);
            Assert.Same(destinationCondition, actualDestinationCondition);
        }

        [Fact]
        public async Task CopyRejectsMissingSource()
        {
            var reference = CreateReference();
            var context = new TestContext(reference.ContentLength, reference.ETag);
            context.Blob
                .Setup(x => x.FetchAttributesAsync())
                .ThrowsAsync(new CloudBlobNotFoundException(null));

            await Assert.ThrowsAsync<CloudBlobNotFoundException>(
                () => context.CreateService().CopyAsync(
                    reference,
                    CoreConstants.Folders.ValidationFolderName,
                    "validation-sets/package.nupkg",
                    destinationAccessCondition: null));
        }

        [Fact]
        public async Task DeleteUsesExpectedETag()
        {
            var context = new TestContext(contentLength: 0, ETag);
            IAccessCondition deleteCondition = null;
            context.Blob
                .Setup(x => x.DeleteIfExistsAsync(It.IsAny<IAccessCondition>()))
                .Callback<IAccessCondition>(condition => deleteCondition = condition)
                .ReturnsAsync(true);

            await context.CreateService().DeleteAsync(BlobPath, ETag);

            Assert.Equal(ETag, deleteCondition.IfMatchETag);
        }

        [Fact]
        public async Task DeleteTreatsMissingBlobAsSuccess()
        {
            var context = new TestContext(contentLength: 0, ETag);
            context.Blob
                .Setup(x => x.DeleteIfExistsAsync(It.IsAny<IAccessCondition>()))
                .ReturnsAsync(false);

            await context.CreateService().DeleteAsync(BlobPath, ETag);
        }

        [Fact]
        public async Task DeleteRejectsETagMismatch()
        {
            var context = new TestContext(contentLength: 0, ETag);
            context.Blob
                .Setup(x => x.DeleteIfExistsAsync(It.IsAny<IAccessCondition>()))
                .ThrowsAsync(new CloudBlobPreconditionFailedException(null));

            await Assert.ThrowsAsync<CloudBlobPreconditionFailedException>(
                () => context.CreateService().DeleteAsync(BlobPath, ETag));
        }

        private static StagingBlobReference CreateReference()
        {
            return new StagingBlobReference(
                BlobPath,
                ETag,
                "content-hash",
                contentLength: 3,
                StagingBlobType.Nupkg);
        }

        private static string GetHash(byte[] content)
        {
            using (var hash = SHA512.Create())
            {
                return Convert.ToBase64String(hash.ComputeHash(content));
            }
        }

        private class TestContext
        {
            public TestContext(long contentLength, string etag)
            {
                Metadata = new Dictionary<string, string>();
                Properties = new Mock<ICloudBlobProperties>();
                Properties.SetupProperty(x => x.ContentType);
                Properties.SetupProperty(x => x.CacheControl);
                Properties.SetupGet(x => x.Length).Returns(contentLength);

                Blob = new Mock<ISimpleCloudBlob>();
                Blob.SetupGet(x => x.Properties).Returns(Properties.Object);
                Blob.SetupGet(x => x.Metadata).Returns(Metadata);
                Blob.SetupGet(x => x.ETag).Returns(etag);
                Blob.Setup(x => x.FetchAttributesAsync()).Returns(Task.CompletedTask);

                Container = new Mock<ICloudBlobContainer>();
                Container.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(Blob.Object);
                Container
                    .Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>()))
                    .Returns(Task.CompletedTask);

                Client = new Mock<ICloudBlobClient>();
                Client
                    .Setup(x => x.GetContainerReference(CoreConstants.Folders.StagingFolderName))
                    .Returns(Container.Object);
            }

            public Mock<ICloudBlobClient> Client { get; }

            public Mock<ICloudBlobContainer> Container { get; }

            public Mock<ISimpleCloudBlob> Blob { get; }

            public Mock<ICloudBlobProperties> Properties { get; }

            public IDictionary<string, string> Metadata { get; }

            public StagingBlobService CreateService(bool initializeContainer = false)
            {
                return new StagingBlobService(Client.Object, initializeContainer);
            }

            public void SetExpectedMetadata(StagingBlobReference reference)
            {
                Properties.Object.ContentType = CoreConstants.PackageContentType;
                Properties.Object.CacheControl = null;
                Metadata[CoreConstants.Sha512HashAlgorithmId] = reference.ContentHash;
                Metadata[StagingBlobService.ArtifactTypeMetadataKey] = "nupkg";
                Metadata[StagingBlobService.FormatVersionMetadataKey] = StagingBlobService.FormatVersion;
            }

            public Mock<ISimpleCloudBlob> CreateDestination(StagingBlobReference source)
            {
                var properties = new Mock<ICloudBlobProperties>();
                properties.SetupGet(x => x.Length).Returns(source.ContentLength);
                properties.SetupGet(x => x.ContentType).Returns(CoreConstants.PackageContentType);
                properties.SetupGet(x => x.CacheControl).Returns((string)null);

                var metadata = new Dictionary<string, string>
                {
                    { CoreConstants.Sha512HashAlgorithmId, source.ContentHash },
                    { StagingBlobService.ArtifactTypeMetadataKey, "nupkg" },
                    { StagingBlobService.FormatVersionMetadataKey, StagingBlobService.FormatVersion },
                };
                var copyState = new Mock<ICloudBlobCopyState>();
                copyState.SetupGet(x => x.Status).Returns(CloudBlobCopyStatus.Success);
                var destination = new Mock<ISimpleCloudBlob>();
                destination.SetupGet(x => x.Properties).Returns(properties.Object);
                destination.SetupGet(x => x.Metadata).Returns(metadata);
                destination.SetupGet(x => x.CopyState).Returns(copyState.Object);

                var destinationContainer = new Mock<ICloudBlobContainer>();
                destinationContainer
                    .Setup(x => x.GetBlobReference(It.IsAny<string>()))
                    .Returns(destination.Object);
                Client
                    .Setup(x => x.GetContainerReference(CoreConstants.Folders.ValidationFolderName))
                    .Returns(destinationContainer.Object);

                return destination;
            }
        }
    }
}

﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NuGetGallery.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery
{
    [Collection(nameof(BlobStorageCollection))]
    public class CloudBlobCoreFileStorageServiceIntegrationTests
    {
        private delegate Task CopyAsync(
            CloudBlobCoreFileStorageService srcService,
            string srcFolderName,
            string srcFileName,
            CloudBlobCoreFileStorageService destService,
            string destFolderName,
            string destFileName);

        private readonly BlobStorageFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly string _testId;
        private readonly string _prefixA;
        private readonly string _prefixB;
        private readonly CloudBlobClientWrapper _clientA;
        private readonly CloudBlobClientWrapper _clientB;
        private readonly CloudBlobClient _blobClientA;
        private readonly CloudBlobClient _blobClientB;
        private readonly CloudBlobCoreFileStorageService _targetA;
        private readonly CloudBlobCoreFileStorageService _targetB;

        public CloudBlobCoreFileStorageServiceIntegrationTests(BlobStorageFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _testId = Guid.NewGuid().ToString();
            _prefixA = $"{_fixture.PrefixA}/{_testId}";
            _prefixB = $"{_fixture.PrefixB}/{_testId}";

            _clientA = new CloudBlobClientWrapper(_fixture.ConnectionStringA, readAccessGeoRedundant: false);
            _clientB = new CloudBlobClientWrapper(_fixture.ConnectionStringB, readAccessGeoRedundant: false);

            _blobClientA = CloudStorageAccount.Parse(_fixture.ConnectionStringA).CreateCloudBlobClient();
            _blobClientB = CloudStorageAccount.Parse(_fixture.ConnectionStringB).CreateCloudBlobClient();

            var folderInformationProvider = new GalleryCloudBlobContainerInformationProvider();

            _targetA = new CloudBlobCoreFileStorageService(_clientA, Mock.Of<IDiagnosticsService>(), folderInformationProvider);
            _targetB = new CloudBlobCoreFileStorageService(_clientB, Mock.Of<IDiagnosticsService>(), folderInformationProvider);
        }

        [BlobStorageFact]
        public async Task EnumeratesBlobs()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var blobAName = $"{_prefixA}/a.txt";
            var blobBName = $"{_prefixA}/b.txt";
            var blobCName = $"{_prefixA}/c.txt";
            await _targetA.SaveFileAsync(folderName, blobAName, new MemoryStream(Encoding.UTF8.GetBytes("A")));
            await _targetA.SaveFileAsync(folderName, blobCName, new MemoryStream(Encoding.UTF8.GetBytes("C")));
            await _targetA.SaveFileAsync(folderName, blobBName, new MemoryStream(Encoding.UTF8.GetBytes("B")));

            var container = _clientA.GetContainerReference(folderName);

            // Act
            var segmentA = await container.ListBlobsSegmentedAsync(
                _prefixA,
                useFlatBlobListing: true,
                blobListingDetails: BlobListingDetails.None,
                maxResults: 2,
                blobContinuationToken: null,
                options: null,
                operationContext: null,
                cancellationToken: CancellationToken.None);
            var segmentB = await container.ListBlobsSegmentedAsync(
                _prefixA,
                useFlatBlobListing: true,
                blobListingDetails: BlobListingDetails.None,
                maxResults: 2,
                blobContinuationToken: segmentA.ContinuationToken,
                options: null,
                operationContext: null,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal(2, segmentA.Results.Count);
            Assert.Equal(blobAName, segmentA.Results[0].Name);
            Assert.Equal(blobBName, segmentA.Results[1].Name);
            Assert.Equal(blobCName, Assert.Single(segmentB.Results).Name);
        }

        [BlobStorageFact]
        public async Task EnumeratesSnapshots()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var blobAName = $"{_prefixA}/a.txt";
            var blobBName = $"{_prefixA}/b.txt";
            await _targetA.SaveFileAsync(folderName, blobAName, new MemoryStream(Encoding.UTF8.GetBytes("A")));
            await _targetA.SaveFileAsync(folderName, blobBName, new MemoryStream(Encoding.UTF8.GetBytes("B")));

            var container = _clientA.GetContainerReference(folderName);
            var blobA = container.GetBlobReference(blobAName);
            await blobA.SnapshotAsync(CancellationToken.None);

            // Act
            var segmentA = await container.ListBlobsSegmentedAsync(
                _prefixA,
                useFlatBlobListing: true,
                blobListingDetails: BlobListingDetails.Snapshots,
                maxResults: 2,
                blobContinuationToken: null,
                options: null,
                operationContext: null,
                cancellationToken: CancellationToken.None);
            var segmentB = await container.ListBlobsSegmentedAsync(
                _prefixA,
                useFlatBlobListing: true,
                blobListingDetails: BlobListingDetails.Snapshots,
                maxResults: 2,
                blobContinuationToken: segmentA.ContinuationToken,
                options: null,
                operationContext: null,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal(2, segmentA.Results.Count);
            Assert.Equal(blobAName, segmentA.Results[0].Name);
            Assert.True(segmentA.Results[0].IsSnapshot);
            Assert.Equal(blobAName, segmentA.Results[1].Name);
            Assert.False(segmentA.Results[1].IsSnapshot);
            Assert.Equal(blobBName, Assert.Single(segmentB.Results).Name);
            Assert.False(segmentB.Results[0].IsSnapshot);
        }

        [BlobStorageFact]
        public async Task AllowsDefaultRequestOptionsToBeSet()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            await _targetA.SaveFileAsync(
                folderName,
                fileName,
                new MemoryStream(new byte[1024 * 1024]),
                overwrite: false);
            var client = new CloudBlobClientWrapper(
                _fixture.ConnectionStringA,
                new BlobRequestOptions
                {
                    MaximumExecutionTime = TimeSpan.FromMilliseconds(1),
                });
            var container = client.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);
            var destination = new MemoryStream();

            // Act & Assert
            // This should throw due to timeout.
            var ex = await Assert.ThrowsAsync<StorageException>(() => file.DownloadToStreamAsync(destination));
            Assert.Contains("timeout", ex.Message);
        }

        [BlobStorageFact]
        public async Task OpenWriteAsyncReturnsWritableStream()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            var expectedContent = "Hello, world.";
            var bytes = Encoding.UTF8.GetBytes(expectedContent);
            string expectedContentMD5;
#pragma warning disable CA5351  
            using (var md5 = MD5.Create())
            {
                expectedContentMD5 = Convert.ToBase64String(md5.ComputeHash(bytes));
            }
#pragma warning disable CA5351 

            var container = _clientA.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);

            // Act
            using (var stream = await file.OpenWriteAsync(accessCondition: null))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }

            // Assert
            // Reinitialize the blob to verify the metadata is fresh.
            file = container.GetBlobReference(fileName);
            using (var memoryStream = new MemoryStream())
            {
                await file.DownloadToStreamAsync(memoryStream);
                var actualContent = Encoding.ASCII.GetString(memoryStream.ToArray());
                Assert.Equal(expectedContent, actualContent);

                Assert.NotNull(file.ETag);
                Assert.NotEmpty(file.ETag);
                Assert.Equal(expectedContentMD5, file.Properties.ContentMD5);
            }
        }

        [BlobStorageFact]
        public async Task UpdateBlobMetadataWithSha512()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            var expectedContent = "Hello, world.";
            var expectedSha512 = "AD0C37C31D69B315F3A81F13C8CDE701094AD91725BA1B0DC3199CA9713661B8280" +
                "D6EF7E68F133E6211E2E5A9A3150445D76F1708E04521B0EE034F0B0BAF26";
            var bytes = Encoding.UTF8.GetBytes(expectedContent);

            var container = _clientA.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);

            // Act
            using (var stream = await file.OpenWriteAsync(accessCondition: null))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }

            await _targetA.SetMetadataAsync(folderName, fileName, (lazyStream, metadata) =>
                {
                    metadata[CoreConstants.Sha512HashAlgorithmId] = expectedSha512;
                    return Task.FromResult(true);
                });

            // Assert
            await file.FetchAttributesAsync();

            Assert.NotNull(file.Metadata[CoreConstants.Sha512HashAlgorithmId]);
            Assert.Equal(expectedSha512, file.Metadata[CoreConstants.Sha512HashAlgorithmId]);
        }

        [BlobStorageFact]
        public async Task OpenWriteAsyncRejectsETagMismatchFoundBeforeUploadStarts()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            var expectedContent = "Hello, world.";

            await _targetA.SaveFileAsync(
                folderName,
                fileName,
                new MemoryStream(Encoding.ASCII.GetBytes(expectedContent)),
                overwrite: false);

            var container = _clientA.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<StorageException>(
                async () =>
                {
                    using (var stream = await file.OpenWriteAsync(AccessCondition.GenerateIfNotExistsCondition()))
                    {
                        await stream.WriteAsync(new byte[0], 0, 0);
                    }
                });
            Assert.Equal(HttpStatusCode.Conflict, (HttpStatusCode)ex.RequestInformation.HttpStatusCode);
        }

        [BlobStorageFact]
        public async Task OpenWriteAsyncRejectsETagMismatchFoundAfterUploadStarts()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            var expectedContent = "Hello, world.";

            var container = _clientA.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);
            var writeCount = 0;

            // Act & Assert
            var ex = await Assert.ThrowsAsync<StorageException>(
                async () =>
                {
                    using (var stream = await file.OpenWriteAsync(AccessCondition.GenerateIfNotExistsCondition()))
                    {
                        stream.Write(new byte[1], 0, 1);
                        await stream.FlushAsync();
                        writeCount++;

                        await _targetA.SaveFileAsync(
                            folderName,
                            fileName,
                            new MemoryStream(Encoding.ASCII.GetBytes(expectedContent)),
                            overwrite: false);

                        stream.Write(new byte[1], 0, 1);
                        await stream.FlushAsync();
                        writeCount++;
                    }
                });
            Assert.Equal(HttpStatusCode.Conflict, (HttpStatusCode)ex.RequestInformation.HttpStatusCode);
            Assert.Equal(2, writeCount);
        }

        [BlobStorageFact]
        public async Task OpenReadAsyncReturnsReadableStreamWhenBlobExistsAndPopulatesProperties()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            var expectedContent = "Hello, world.";

            await _targetA.SaveFileAsync(
                folderName,
                fileName,
                new MemoryStream(Encoding.ASCII.GetBytes(expectedContent)),
                overwrite: false);

            var container = _clientA.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);

            // Act
            using (var stream = await file.OpenReadAsync(accessCondition: null))
            using (var streamReader = new StreamReader(stream))
            {
                var actualContent = await streamReader.ReadToEndAsync();

                // Assert
                Assert.Equal(expectedContent, actualContent);
                Assert.Equal(expectedContent.Length, file.Properties.Length);
                Assert.NotNull(file.ETag);
            }
        }

        [BlobStorageFact]
        public async Task OpenReadAsyncThrowsNotFoundWhenBlobDoesNotExist()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            var exists = await _targetA.FileExistsAsync(folderName, fileName);
            var container = _clientA.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);

            // Act & Assert
            Assert.False(exists);
            var ex = await Assert.ThrowsAsync<StorageException>(
                () => file.OpenReadAsync(accessCondition: null));
            Assert.Equal(HttpStatusCode.NotFound, (HttpStatusCode)ex.RequestInformation.HttpStatusCode);
        }

        [BlobStorageFact]
        public async Task OpenReadAsyncThrowsPreconditionFailedWhenIfMatchFails()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;

            await _targetA.SaveFileAsync(
                folderName,
                fileName,
                new MemoryStream(Encoding.ASCII.GetBytes("Hello, world.")),
                overwrite: false);

            var container = _clientA.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);
            await file.FetchAttributesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<StorageException>(
                () => file.OpenReadAsync(accessCondition: AccessCondition.GenerateIfMatchCondition("WON'T MATCH")));
            Assert.Equal(HttpStatusCode.PreconditionFailed, (HttpStatusCode)ex.RequestInformation.HttpStatusCode);
        }

        [BlobStorageFact]
        public async Task OpenReadAsyncThrowsNotModifiedWhenIfNoneMatchFails()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;

            await _targetA.SaveFileAsync(
                folderName,
                fileName,
                new MemoryStream(Encoding.ASCII.GetBytes("Hello, world.")),
                overwrite: false);

            var container = _clientA.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);
            await file.FetchAttributesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<StorageException>(
                () => file.OpenReadAsync(accessCondition: AccessCondition.GenerateIfNoneMatchCondition(file.ETag)));
            Assert.Equal(HttpStatusCode.NotModified, (HttpStatusCode)ex.RequestInformation.HttpStatusCode);
        }

        [BlobStorageFact]
        public async Task OpenReadAsyncReturnsContentWhenIfNoneMatchSucceeds()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            var expectedContent = "Hello, world.";

            await _targetA.SaveFileAsync(
                folderName,
                fileName,
                new MemoryStream(Encoding.ASCII.GetBytes(expectedContent)),
                overwrite: false);

            var container = _clientA.GetContainerReference(folderName);
            var file = container.GetBlobReference(fileName);

            // Act
            using (var stream = await file.OpenReadAsync(accessCondition: AccessCondition.GenerateIfNoneMatchCondition("WON'T MATCH")))
            using (var streamReader = new StreamReader(stream))
            {
                var actualContent = await streamReader.ReadToEndAsync();

                // Assert
                Assert.Equal(expectedContent, actualContent);
                Assert.Equal(expectedContent.Length, file.Properties.Length);
                Assert.NotNull(file.ETag);
            }
        }

        [BlobStorageFact]
        public async Task ReturnsCurrentETagForIfMatch()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            await _targetA.SaveFileAsync(folderName, fileName, new MemoryStream(new byte[0]));
            var initialReference = await _targetA.GetFileReferenceAsync(folderName, fileName);
            initialReference.OpenRead().Dispose();

            // Act
            var reference = await _targetA.GetFileReferenceAsync(folderName, fileName, initialReference.ContentId);

            // Assert
            Assert.NotNull(reference);
            Assert.Null(reference.OpenRead());
            Assert.Equal(initialReference.ContentId, reference.ContentId);
        }

        [BlobStorageFact]
        public async Task ReturnsNullForMissingBlob()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;

            // Act
            var reference = await _targetA.GetFileReferenceAsync(folderName, fileName);

            // Assert
            Assert.Null(reference);
        }

        [BlobStorageFact]
        public async Task ReturnsTheETagMatchingTheContent()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            var contentToETag = new ConcurrentDictionary<string, string>();
            var iterations = 20;
            var cts = new CancellationTokenSource();

            Func<Task> update = async () =>
            {
                var container = _blobClientA.GetContainerReference(folderName);
                for (var i = 1; i <= iterations && !cts.IsCancellationRequested; i++)
                {
                    var blob = container.GetBlockBlobReference(fileName);
                    var content = i.ToString();
                    await blob.UploadTextAsync(content);
                    contentToETag[content] = blob.Properties.ETag;
                    _output.WriteLine($"Content '{content}' should have etag '{blob.Properties.ETag}'.");
                }
            };

            Func<Task> check = async () =>
            {
                string content = null;
                while (content != iterations.ToString())
                {
                    var fileReference = await _targetA.GetFileReferenceAsync(folderName, fileName);
                    if (fileReference == null)
                    {
                        continue;
                    }

                    using (var stream = fileReference.OpenRead())
                    using (var streamReader = new StreamReader(stream))
                    {
                        content = await streamReader.ReadToEndAsync();
                        if (contentToETag.TryGetValue(content, out var expectedETag))
                        {
                            _output.WriteLine($"Content '{content}' has etag '{fileReference.ContentId}'.");
                            if (expectedETag != fileReference.ContentId)
                            {
                                cts.Cancel();
                            }

                            Assert.Equal(expectedETag, fileReference.ContentId);
                        }
                    }
                }
            };

            // Act & Assert
            var updateTask = update();
            var checkTask = check();
            await checkTask;
            await updateTask;
        }

        [BlobStorageFact]
        public async Task CanReadAndDeleteBlobUsingPrivilegedFileUri()
        {
            // Arrange
            var folderName = CoreConstants.Folders.ValidationFolderName;
            var fileName = _prefixA;
            var expectedContent = "Hello, world.";

            await _targetA.SaveFileAsync(
                folderName,
                fileName,
                new MemoryStream(Encoding.ASCII.GetBytes(expectedContent)),
                overwrite: false);

            var deleteUri = await _targetA.GetPriviledgedFileUriAsync(
                folderName,
                fileName,
                FileUriPermissions.Read | FileUriPermissions.Delete,
                DateTimeOffset.UtcNow.AddHours(1));

            // Act
            var sasToken = new StorageCredentials(deleteUri.Query);
            var deleteUriBuilder = new UriBuilder(deleteUri) { Query = null };
            var blob = new CloudBlockBlob(deleteUriBuilder.Uri, sasToken);

            var actualContent = await blob.DownloadTextAsync();
            await blob.DeleteAsync();

            // Assert
            Assert.Equal(expectedContent, actualContent);
            var exists = await _targetA.FileExistsAsync(folderName, fileName);
            Assert.False(exists, "The file should no longer exist.");
        }

        [BlobStorageFact]
        public async Task CopyingWithUriWorksWithinTheSameStorageAccount()
        {
            await CopyFileWorksAsync(CopyFileWithUriAsync, _prefixA, _targetA, _prefixA, _targetA);
        }

        [BlobStorageFact]
        public async Task CopyingWithUriWorksWithDifferentStorageAccounts()
        {
            await CopyFileWorksAsync(CopyFileWithUriAsync, _prefixA, _targetA, _prefixB, _targetB);
        }

        [BlobStorageFact]
        public async Task CopyingWithNamesWorksWithinTheSameStorageAccount()
        {
            await CopyFileWorksAsync(CopyFileWithNamesAsync, _prefixA, _targetA, _prefixA, _targetA);
        }

        [BlobStorageFact]
        public async Task DoesNotCopyWhenSourceAndDestinationHaveSameHash()
        {
            // Arrange
            var srcFolderName = CoreConstants.Folders.ValidationFolderName;
            var srcFileName = $"{_prefixA}/src";
            var srcContent = "Hello, world.";
            var srcSha512 = "AD0C37C31D69B315F3A81F13C8CDE701094AD91725BA1B0DC3199CA9713661B8280" +
                "D6EF7E68F133E6211E2E5A9A3150445D76F1708E04521B0EE034F0B0BAF26";

            var destFolderName = CoreConstants.Folders.PackagesFolderName;
            var destFileName = $"{_prefixB}/dest";

            await _targetA.SaveFileAsync(
                srcFolderName,
                srcFileName,
                new MemoryStream(Encoding.ASCII.GetBytes(srcContent)),
                overwrite: false);

            await _targetA.SetMetadataAsync(srcFolderName, srcFileName, (lazyStream, metadata) =>
            {
                metadata[CoreConstants.Sha512HashAlgorithmId] = srcSha512;
                return Task.FromResult(true);
            });

            await _targetB.SaveFileAsync(
                destFolderName,
                destFileName,
                new MemoryStream(Encoding.ASCII.GetBytes(srcContent)),
                overwrite: false);

            await _targetB.SetMetadataAsync(destFolderName, destFileName, (lazyStream, metadata) =>
            {
                metadata[CoreConstants.Sha512HashAlgorithmId] = srcSha512;
                return Task.FromResult(true);
            });

            var originalDestFileReference = await _targetB.GetFileReferenceAsync(destFolderName, destFileName);
            var originalDestETag = originalDestFileReference.ContentId;

            var srcUri = await _targetA.GetFileReadUriAsync(
                srcFolderName,
                srcFileName,
                DateTimeOffset.UtcNow.AddHours(1));

            var destAccessCondition = AccessConditionWrapper.GenerateIfNotExistsCondition();

            // Act
            await _targetB.CopyFileAsync(
                srcUri,
                destFolderName,
                destFileName,
                destAccessCondition);

            // Assert
            var finalDestFileReference = await _targetB.GetFileReferenceAsync(destFolderName, destFileName);
            var finalDestETag = finalDestFileReference.ContentId;
            Assert.Equal(originalDestETag, finalDestETag);
        }

        [BlobStorageFact]
        public async Task CopiesWhenDestinationHasNotHashButContentsAreTheSame()
        {
            // Arrange
            var srcFolderName = CoreConstants.Folders.ValidationFolderName;
            var srcFileName = $"{_prefixA}/src";
            var srcContent = "Hello, world.";
            var srcSha512 = "AD0C37C31D69B315F3A81F13C8CDE701094AD91725BA1B0DC3199CA9713661B8280" +
                "D6EF7E68F133E6211E2E5A9A3150445D76F1708E04521B0EE034F0B0BAF26";

            var destFolderName = CoreConstants.Folders.PackagesFolderName;
            var destFileName = $"{_prefixB}/dest";

            await _targetA.SaveFileAsync(
                srcFolderName,
                srcFileName,
                new MemoryStream(Encoding.ASCII.GetBytes(srcContent)),
                overwrite: false);

            await _targetA.SetMetadataAsync(srcFolderName, srcFileName, (lazyStream, metadata) =>
            {
                metadata[CoreConstants.Sha512HashAlgorithmId] = srcSha512;
                return Task.FromResult(true);
            });

            await _targetB.SaveFileAsync(
                destFolderName,
                destFileName,
                new MemoryStream(Encoding.ASCII.GetBytes(srcContent)),
                overwrite: false);

            var originalDestFileReference = await _targetB.GetFileReferenceAsync(destFolderName, destFileName);
            var originalDestETag = originalDestFileReference.ContentId;

            var srcUri = await _targetA.GetFileReadUriAsync(
                srcFolderName,
                srcFileName,
                DateTimeOffset.UtcNow.AddHours(1));

            var destAccessCondition = AccessConditionWrapper.GenerateEmptyCondition();

            // Act
            await _targetB.CopyFileAsync(
                srcUri,
                destFolderName,
                destFileName,
                destAccessCondition);

            // Assert
            var finalDestFileReference = await _targetB.GetFileReferenceAsync(destFolderName, destFileName);
            var finalDestETag = finalDestFileReference.ContentId;
            Assert.NotEqual(originalDestETag, finalDestETag);
        }

        private static CloudBlockBlob GetBlob(CloudBlobClient blobClient, string folderName, string fileName)
        {
            return blobClient.GetContainerReference(folderName).GetBlockBlobReference(fileName);
        }

        private static async Task CopyFileWorksAsync(
            CopyAsync copyAsync,
            string srcPrefix,
            CloudBlobCoreFileStorageService srcService,
            string destPrefix,
            CloudBlobCoreFileStorageService destService)
        {
            // Arrange
            var srcFolderName = CoreConstants.Folders.ValidationFolderName;
            var srcFileName = $"{srcPrefix}/src";
            var srcContent = "Hello, world.";

            var destFolderName = CoreConstants.Folders.PackagesFolderName;
            var destFileName = $"{destPrefix}/dest";

            await srcService.SaveFileAsync(
                srcFolderName,
                srcFileName,
                new MemoryStream(Encoding.ASCII.GetBytes(srcContent)),
                overwrite: false);

            // Act
            await copyAsync(srcService, srcFolderName, srcFileName, destService, destFolderName, destFileName);

            // Assert
            using (var destStream = await destService.GetFileAsync(destFolderName, destFileName))
            using (var destReader = new StreamReader(destStream))
            {
                var destContent = destReader.ReadToEnd();
                Assert.Equal(srcContent, destContent);
            }
        }

        private static async Task CopyFileWithNamesAsync(
            CloudBlobCoreFileStorageService srcService,
            string srcFolderName,
            string srcFileName,
            CloudBlobCoreFileStorageService destService,
            string destFolderName,
            string destFileName)
        {
            var destAccessCondition = AccessConditionWrapper.GenerateIfNotExistsCondition();

            await destService.CopyFileAsync(
                srcFolderName,
                srcFileName,
                destFolderName,
                destFileName,
                destAccessCondition);
        }

        private static async Task CopyFileWithUriAsync(
            CloudBlobCoreFileStorageService srcService,
            string srcFolderName,
            string srcFileName,
            CloudBlobCoreFileStorageService destService,
            string destFolderName,
            string destFileName)
        {
            var endOfAccess = DateTimeOffset.UtcNow.AddHours(1);
            var srcUri = await srcService.GetFileReadUriAsync(srcFolderName, srcFileName, endOfAccess);
            var destAccessCondition = AccessConditionWrapper.GenerateIfNotExistsCondition();

            await destService.CopyFileAsync(
                srcUri,
                destFolderName,
                destFileName,
                destAccessCondition);
        }
    }
}

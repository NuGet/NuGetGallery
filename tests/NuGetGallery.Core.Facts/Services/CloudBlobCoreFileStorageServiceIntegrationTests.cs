﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
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

            _targetA = new CloudBlobCoreFileStorageService(_clientA, Mock.Of<IDiagnosticsService>());
            _targetB = new CloudBlobCoreFileStorageService(_clientB, Mock.Of<IDiagnosticsService>());
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

            var destFolderName = CoreConstants.Folders.PackagesFolderName;
            var destFileName = $"{_prefixB}/dest";

            await _targetA.SaveFileAsync(
                srcFolderName,
                srcFileName,
                new MemoryStream(Encoding.ASCII.GetBytes(srcContent)),
                overwrite: false);

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

            var destFolderName = CoreConstants.Folders.PackagesFolderName;
            var destFileName = $"{_prefixB}/dest";

            await _targetA.SaveFileAsync(
                srcFolderName,
                srcFileName,
                new MemoryStream(Encoding.ASCII.GetBytes(srcContent)),
                overwrite: false);

            await _targetB.SaveFileAsync(
                destFolderName,
                destFileName,
                new MemoryStream(Encoding.ASCII.GetBytes(srcContent)),
                overwrite: false);

            var originalDestFileReference = await _targetB.GetFileReferenceAsync(destFolderName, destFileName);
            var originalDestETag = originalDestFileReference.ContentId;

            await ClearContentMD5(_blobClientB, destFolderName, destFileName);

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

        private async Task ClearContentMD5(CloudBlobClient blobClient, string folderName, string fileName)
        {
            var blob = GetBlob(blobClient, folderName, fileName);
            await blob.FetchAttributesAsync();
            blob.Properties.ContentMD5 = null;
            await blob.SetPropertiesAsync();
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

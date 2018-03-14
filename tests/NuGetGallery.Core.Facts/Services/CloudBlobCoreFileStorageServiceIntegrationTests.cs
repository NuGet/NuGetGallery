// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

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
        private readonly string _testId;
        private readonly string _prefixA;
        private readonly string _prefixB;
        private readonly CloudBlobClientWrapper _clientA;
        private readonly CloudBlobClientWrapper _clientB;
        private readonly CloudBlobCoreFileStorageService _targetA;
        private readonly CloudBlobCoreFileStorageService _targetB;

        public CloudBlobCoreFileStorageServiceIntegrationTests(BlobStorageFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _testId = Guid.NewGuid().ToString();
            _prefixA = $"{_fixture.PrefixA}/{_testId}";
            _prefixB = $"{_fixture.PrefixB}/{_testId}";

            _clientA = new CloudBlobClientWrapper(_fixture.ConnectionStringA, readAccessGeoRedundant: false);
            _clientB = new CloudBlobClientWrapper(_fixture.ConnectionStringB, readAccessGeoRedundant: false);

            _targetA = new CloudBlobCoreFileStorageService(_clientA);
            _targetB = new CloudBlobCoreFileStorageService(_clientB);
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

        private static async Task CopyFileWorksAsync(
            CopyAsync copyAsync,
            string srcPrefix,
            CloudBlobCoreFileStorageService srcService,
            string destPrefix,
            CloudBlobCoreFileStorageService destService)
        {
            // Arrange
            var srcFolderName = CoreConstants.ValidationFolderName;
            var srcFileName = $"{srcPrefix}/src";
            var srcContent = "Hello, world.";

            var destFolderName = CoreConstants.PackagesFolderName;
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
            await destService.CopyFileAsync(
                srcFolderName,
                srcFileName,
                destFolderName,
                destFileName,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
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

            await destService.CopyFileAsync(
                srcUri,
                destFolderName,
                destFileName,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
        }
    }
}

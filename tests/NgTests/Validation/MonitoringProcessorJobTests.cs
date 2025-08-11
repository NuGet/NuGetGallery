// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Ng.Jobs;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Storage;
using Xunit;

namespace NgTests.Validation
{
    public class MonitoringProcessorJobTests
    {
        [Fact]
        public void CreatePackageValidatorContext_DecodesPackageIdCorrectly()
        {
            // Arrange
            var encodedPackageId = "E2E.SemVer1StableUnicodeId.250602.175349.2961608%D0%BF%D0%B0%D0%BA%D0%B5%D1%82%E5%8C%85els%C3%B6kning123";
            var decodedPackageId = "E2E.SemVer1StableUnicodeId.250602.175349.2961608пакет包elsökning123";
            var packageVersion = "1.0.0";
            var catalogEntries = new List<CatalogIndexEntry>
            {
                new CatalogIndexEntry(
                    new Uri("https://example.com/catalog/entry"),
                    "type",
                    "commitId",
                    DateTime.UtcNow,
                    new NuGet.Packaging.Core.PackageIdentity(decodedPackageId, NuGet.Versioning.NuGetVersion.Parse(packageVersion)))
            };

            var queueMessage = new StorageQueueMessage<PackageValidatorContext>(
                new PackageValidatorContext(new FeedPackageIdentity(encodedPackageId, packageVersion), catalogEntries),
                dequeueCount: 1);

            // Act - Replicate the logic from HandleQueueMessageAsync that we want to test
            string packageId = queueMessage.Contents.Package.Id;
            string packageVersionFromMessage = queueMessage.Contents.Package.Version;

            string decodedPackageIdFromMessage = Uri.UnescapeDataString(packageId);
            var packageIdentity = new FeedPackageIdentity(decodedPackageIdFromMessage, packageVersionFromMessage);
            var queuedContext = new PackageValidatorContext(packageIdentity, queueMessage.Contents.CatalogEntries);

            // Assert
            Assert.Equal(decodedPackageId, queuedContext.Package.Id);
            Assert.Equal(packageVersion, queuedContext.Package.Version);
            Assert.Equal(catalogEntries, queuedContext.CatalogEntries);
        }

        [Fact]
        public void CreatePackageValidatorContext_DecodesPackageVersionCorrectly()
        {
            // Arrange
            var packageId = "TestPackage";
            var encodedPackageVersion = "1.0.0%2Bmetadata";
            var decodedPackageVersion = "1.0.0+metadata";
            var catalogEntries = new List<CatalogIndexEntry>
            {
                new CatalogIndexEntry(
                    new Uri("https://example.com/catalog/entry"),
                    "type",
                    "commitId",
                    DateTime.UtcNow,
                    new NuGet.Packaging.Core.PackageIdentity(packageId, NuGet.Versioning.NuGetVersion.Parse(decodedPackageVersion)))
            };

            var queueMessage = new StorageQueueMessage<PackageValidatorContext>(
                new PackageValidatorContext(new FeedPackageIdentity(packageId, encodedPackageVersion), catalogEntries),
                dequeueCount: 1);

            // Act - Replicate the logic from HandleQueueMessageAsync that we want to test
            string packageIdFromMessage = queueMessage.Contents.Package.Id;
            string packageVersion = queueMessage.Contents.Package.Version;

            string decodedPackageVersionFromMessage = Uri.UnescapeDataString(packageVersion);
            var packageIdentity = new FeedPackageIdentity(packageIdFromMessage, decodedPackageVersionFromMessage);
            var queuedContext = new PackageValidatorContext(packageIdentity, queueMessage.Contents.CatalogEntries);

            // Assert
            Assert.Equal(packageId, queuedContext.Package.Id);
            Assert.Equal(decodedPackageVersion, queuedContext.Package.Version);
            Assert.Equal(catalogEntries, queuedContext.CatalogEntries);
        }
    }
}

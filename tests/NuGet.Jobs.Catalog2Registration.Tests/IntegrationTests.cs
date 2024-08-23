// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Catalog;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public class IntegrationTests
    {
        [Fact]
        public async Task AddFirstVersionWhenInlined()
        {
            // Arrange & Act
            await AddVersionsAsync("7.1.2-ALPHA");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new[]
                {
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/index.json",
                },
                missing: new string[0]);
        }

        [Fact]
        public async Task AddSecondVersionWhenInlined()
        {
            // Arrange
            await AddVersionsAsync("7.1.2-ALPHA");

            // Act
            await AddVersionsAsync("7.2.0");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new[]
                {
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/7.2.0.json",
                    "windowsazure.storage/index.json",
                },
                missing: new string[0]);
        }

        [Fact]
        public async Task UpdateExistingVersion()
        {
            // Arrange
            var version = "7.1.2-alpha";
            await AddVersionsAsync(version);

            // Act
            await AddVersionsAsync(version);

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new[]
                {
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/index.json",
                },
                missing: new string[0]);
        }

        [Fact]
        public async Task DeleteWhenNoVersionExists()
        {
            // Arrange & Act
            await DeleteVersionsAsync("7.1.2-ALPHA");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new string[0],
                missing: new[]
                {
                    "windowsazure.storage/index.json",
                });
        }

        [Fact]
        public async Task DeleteWhenVersionDoesNotExist()
        {
            // Arrange
            await AddVersionsAsync("7.2.0");

            // Act
            await DeleteVersionsAsync("7.1.2-ALPHA");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new[]
                {
                    "windowsazure.storage/7.2.0.json",
                    "windowsazure.storage/index.json",
                },
                missing: new string[0]);
        }

        [Fact]
        public async Task DeleteLastVersion()
        {
            // Arrange
            await AddVersionsAsync("7.2.0");

            // Act
            await DeleteVersionsAsync("7.2.0");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new string[0],
                missing: new[]
                {
                    "windowsazure.storage/7.2.0.json",
                    "windowsazure.storage/index.json",
                });
        }

        [Fact]
        public async Task AddItemToTheEndOfAPage()
        {
            // Arrange
            Config.MaxInlinedLeafItems = 0;
            await AddVersionsAsync("7.1.2-ALPHA");

            // Act
            await AddVersionsAsync("8.0.0");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new[]
                {
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/8.0.0.json",
                    "windowsazure.storage/index.json",
                    "windowsazure.storage/page/7.1.2-alpha/8.0.0.json",
                },
                missing: new[]
                {
                    "windowsazure.storage/page/7.1.2-alpha/7.1.2-alpha.json",
                });
        }

        [Fact]
        public async Task InsertItemInTheMiddleOfAPage()
        {
            // Arrange
            Config.MaxInlinedLeafItems = 0;
            await AddVersionsAsync("7.1.2-ALPHA", "8.0.0");

            // Act
            await AddVersionsAsync("7.2.0");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new string[]
                {
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/7.2.0.json",
                    "windowsazure.storage/8.0.0.json",
                    "windowsazure.storage/index.json",
                    "windowsazure.storage/page/7.1.2-alpha/8.0.0.json",
                },
                missing: new string[0]);
        }

        [Fact]
        public async Task DeleteItemInTheMiddleOfAPage()
        {
            // Arrange
            Config.MaxInlinedLeafItems = 0;
            await AddVersionsAsync("7.1.2-ALPHA", "7.2.0", "8.0.0");

            // Act
            await DeleteVersionsAsync("7.2.0");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new[]
                {
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/8.0.0.json",
                    "windowsazure.storage/index.json",
                    "windowsazure.storage/page/7.1.2-alpha/8.0.0.json",
                },
                missing: new[]
                {
                    "windowsazure.storage/7.2.0.json",
                });
        }

        [Fact]
        public async Task DeleteItemAtTheEndOfAPage()
        {
            // Arrange
            Config.MaxInlinedLeafItems = 0;
            await AddVersionsAsync("7.1.2-ALPHA", "8.0.0");

            // Act
            await DeleteVersionsAsync("8.0.0");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new[]
                {
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/index.json",
                    "windowsazure.storage/page/7.1.2-alpha/7.1.2-alpha.json",
                },
                missing: new[]
                {
                    "windowsazure.storage/8.0.0.json",
                    "windowsazure.storage/page/7.1.2-alpha/8.0.0.json",
                });
        }

        [Fact]
        public async Task DeleteItemAtTheEndOfANonLastPage()
        {
            // Arrange
            Config.MaxInlinedLeafItems = 0;
            Config.MaxLeavesPerPage = 2;
            await AddVersionsAsync("7.1.2-ALPHA", "7.2.0", "8.0.0");

            // Act
            await DeleteVersionsAsync("7.2.0");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new[]
                {
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/8.0.0.json",
                    "windowsazure.storage/index.json",
                    "windowsazure.storage/page/7.1.2-alpha/8.0.0.json",
                },
                missing: new[]
                {
                    "windowsazure.storage/7.2.0.json",
                    "windowsazure.storage/page/7.1.2-alpha/7.2.0.json",
                    "windowsazure.storage/page/8.0.0/8.0.0.json",
                });
        }

        [Fact]
        public async Task DeleteFirstPage()
        {
            // Arrange
            Config.MaxInlinedLeafItems = 0;
            Config.MaxLeavesPerPage = 2;
            await AddVersionsAsync("7.1.2-ALPHA", "7.2.0", "8.0.0");

            // Act
            await DeleteVersionsAsync("7.1.2-ALPHA", "7.2.0");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new[]
                {
                    "windowsazure.storage/8.0.0.json",
                    "windowsazure.storage/index.json",
                    "windowsazure.storage/page/8.0.0/8.0.0.json",
                },
                missing: new[]
                {
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/7.2.0.json",
                    "windowsazure.storage/page/7.1.2-alpha/7.2.0.json",
                });
        }

        [Fact]
        public async Task DeleteLastVersionWhenNotInlined()
        {
            // Arrange
            Config.MaxInlinedLeafItems = 0;
            await AddVersionsAsync("7.1.2-ALPHA");

            // Act
            await DeleteVersionsAsync("7.1.2-ALPHA");

            // Assert
            AssertContainerExistence();
            AssertBlobExistence(
                existing: new string[0],
                missing: new[]
                {
                    "windowsazure.storage/index.json",
                    "windowsazure.storage/7.1.2-alpha.json",
                    "windowsazure.storage/page/7.1.2-alpha/7.1.2-alpha.json",
                });
        }

        public IntegrationTests(ITestOutputHelper output)
        {
            Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
            Config = new Catalog2RegistrationConfiguration
            {
                LegacyBaseUrl = "https://example/v3/reg",
                LegacyStorageContainer = "v3-reg",
                GzippedBaseUrl = "https://example/v3/reg-gz",
                GzippedStorageContainer = "v3-reg-gz",
                SemVer2BaseUrl = "https://example/v3/reg-gz-semver2",
                SemVer2StorageContainer = "v3-reg-gz-semver2",
                FlatContainerBaseUrl = "https://example/v3/flatcontainer",
                GalleryBaseUrl = "https://example-gallery",
                MaxConcurrentHivesPerId = 1,
                MaxConcurrentIds = 1,
                MaxConcurrentOperationsPerHive = 1,
                MaxConcurrentStorageOperations = 1,
                EnsureSingleSnapshot = false,
            };
            Options.Setup(x => x.Value).Returns(() => Config);

            CloudBlobClient = new InMemoryCloudBlobClient();
            RegistrationUrlBuilder = new RegistrationUrlBuilder(Options.Object);
            EntityBuilder = new EntityBuilder(RegistrationUrlBuilder, Options.Object);
            Throttle = NullThrottle.Instance;
            HiveStorage = new HiveStorage(
                CloudBlobClient,
                RegistrationUrlBuilder,
                EntityBuilder,
                Throttle,
                Options.Object,
                output.GetLogger<HiveStorage>());
            HiveMerger = new HiveMerger(Options.Object, output.GetLogger<HiveMerger>());
            HiveUpdater = new HiveUpdater(
                HiveStorage,
                HiveMerger,
                EntityBuilder,
                Options.Object,
                output.GetLogger<HiveUpdater>());
            RegistrationUpdater = new RegistrationUpdater(
                HiveUpdater,
                Options.Object,
                output.GetLogger<RegistrationUpdater>());
        }

        public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
        public Catalog2RegistrationConfiguration Config { get; }
        public InMemoryCloudBlobClient CloudBlobClient { get; }
        public RegistrationUrlBuilder RegistrationUrlBuilder { get; }
        public EntityBuilder EntityBuilder { get; }
        public NullThrottle Throttle { get; }
        public HiveStorage HiveStorage { get; }
        public HiveMerger HiveMerger { get; }
        public HiveUpdater HiveUpdater { get; }
        public RegistrationUpdater RegistrationUpdater { get; }

        private void AssertContainerExistence()
        {
            Assert.Equal(
                new[] { Config.LegacyStorageContainer, Config.GzippedStorageContainer, Config.SemVer2StorageContainer },
                CloudBlobClient.Containers.Keys.OrderBy(x => x).ToArray());
        }

        private void AssertBlobExistence(IReadOnlyList<string> existing, IReadOnlyList<string> missing)
        {
            var allBlobs = existing.Concat(missing).OrderBy(x => x).ToArray();
            Assert.All(CloudBlobClient.Containers.Values, b => Assert.Equal(allBlobs, b.Blobs.Keys.ToArray()));
            Assert.All(
                CloudBlobClient.Containers.SelectMany(c => c.Value.Blobs).Where(b => !missing.Contains(b.Key)),
                b => Assert.True(b.Value.Exists));
            Assert.All(
                CloudBlobClient.Containers.SelectMany(c => missing.Select(d => c.Value.Blobs[d])),
                b => Assert.False(b.Exists));
        }

        private CatalogCommitItem GetPackageDetailsItem(string id, NuGetVersion version)
        {
            return new CatalogCommitItem(
                uri: new Uri("https://example/0"),
                commitId: null,
                commitTimeStamp: new DateTime(2018, 1, 1),
                types: null,
                typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                packageIdentity: new PackageIdentity(id, version));
        }

        private CatalogCommitItem GetPackageDeleteItem(string id, NuGetVersion version)
        {
            return new CatalogCommitItem(
                uri: new Uri("https://example/0"),
                commitId: null,
                commitTimeStamp: new DateTime(2018, 1, 1),
                types: null,
                typeUris: new List<Uri> { Schema.DataTypes.PackageDelete },
                packageIdentity: new PackageIdentity(id, version));
        }

        private async Task DeleteVersionsAsync(params string[] versions)
        {
            var entries = new List<CatalogCommitItem>();
            var entryToLeaf = new Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>();

            foreach (var version in versions)
            {
                var parsedVersion = NuGetVersion.Parse(version);
                var catalogItem = GetPackageDeleteItem(V3Data.PackageId, parsedVersion);
                entries.Add(catalogItem);
            }

            await RegistrationUpdater.UpdateAsync(V3Data.PackageId, entries, entryToLeaf);
        }

        private async Task AddVersionsAsync(params string[] versions)
        {
            var entries = new List<CatalogCommitItem>();
            var entryToLeaf = new Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>();

            foreach (var version in versions)
            {
                var parsedVersion = NuGetVersion.Parse(version);
                var catalogItem = GetPackageDetailsItem(V3Data.PackageId, parsedVersion);
                var leaf = V3Data.Leaf;
                leaf.PackageVersion = parsedVersion.ToFullString();
                leaf.VerbatimVersion = version;

                entries.Add(catalogItem);
                entryToLeaf.Add(catalogItem, leaf);
            }

            await RegistrationUpdater.UpdateAsync(V3Data.PackageId, entries, entryToLeaf);
        }
    }
}

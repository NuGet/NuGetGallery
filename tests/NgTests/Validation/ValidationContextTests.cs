// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using Xunit;

namespace NgTests.Validation
{
    public class ValidationContextTests
    {
        private static readonly PackageIdentity _packageIdentity = new PackageIdentity("A", new NuGetVersion(1, 0, 0));
        private static readonly ValidationSourceRepositories _mockValidationSourceRepositories =
            new ValidationSourceRepositories(Mock.Of<SourceRepository>(), Mock.Of<SourceRepository>());

        [Fact]
        public void Constructor_WhenPackageIdentityIsNull_Throws()
        {
            PackageIdentity packageIdentity = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    packageIdentity,
                    Enumerable.Empty<CatalogIndexEntry>(),
                    Enumerable.Empty<DeletionAuditEntry>(),
                    _mockValidationSourceRepositories,
                    new CollectorHttpClient(),
                    CancellationToken.None,
                    Mock.Of<ILogger<ValidationContext>>()));

            Assert.Equal("package", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenEntriesIsNull_Throws()
        {
            IEnumerable<CatalogIndexEntry> entries = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    _packageIdentity,
                    entries,
                    Enumerable.Empty<DeletionAuditEntry>(),
                    _mockValidationSourceRepositories,
                    new CollectorHttpClient(),
                    CancellationToken.None,
                    Mock.Of<ILogger<ValidationContext>>()));

            Assert.Equal("entries", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenDeletionAuditEntriesIsNull_Throws()
        {
            IEnumerable<DeletionAuditEntry> deletionAuditEntries = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    _packageIdentity,
                    Enumerable.Empty<CatalogIndexEntry>(),
                    deletionAuditEntries,
                    _mockValidationSourceRepositories,
                    new CollectorHttpClient(),
                    CancellationToken.None,
                    Mock.Of<ILogger<ValidationContext>>()));

            Assert.Equal("deletionAuditEntries", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenFeedToSourceIsNull_Throws()
        {
            ValidationSourceRepositories sourceRepositories = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    _packageIdentity,
                    Enumerable.Empty<CatalogIndexEntry>(),
                    Enumerable.Empty<DeletionAuditEntry>(),
                    sourceRepositories,
                    new CollectorHttpClient(),
                    CancellationToken.None,
                    Mock.Of<ILogger<ValidationContext>>()));

            Assert.Equal("sourceRepositories", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenClientIsNull_Throws()
        {
            CollectorHttpClient client = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    _packageIdentity,
                    Enumerable.Empty<CatalogIndexEntry>(),
                    Enumerable.Empty<DeletionAuditEntry>(),
                    _mockValidationSourceRepositories,
                    client,
                    CancellationToken.None,
                    Mock.Of<ILogger<ValidationContext>>()));

            Assert.Equal("client", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    _packageIdentity,
                    Enumerable.Empty<CatalogIndexEntry>(),
                    Enumerable.Empty<DeletionAuditEntry>(),
                    _mockValidationSourceRepositories,
                    new CollectorHttpClient(),
                    CancellationToken.None,
                    logger: null));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void Package_ReturnsCorrectValue()
        {
            var context = CreateContext(_packageIdentity);

            Assert.Same(_packageIdentity, context.Package);
        }

        [Fact]
        public void Entries_ReturnsCorrectValue()
        {
            var entry = new CatalogIndexEntry(
                new Uri("https://nuget.test"),
                CatalogConstants.NuGetPackageDetails,
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                _packageIdentity);
            var entries = new[] { entry };
            var context = CreateContext(entries: entries);

            Assert.Equal(entries.Length, context.Entries.Count);
            Assert.Same(entry, context.Entries[0]);
        }

        [Fact]
        public void DeletionAuditEntries_ReturnsCorrectValue()
        {
            var entry = new DeletionAuditEntry();
            var entries = new[] { entry };
            var context = CreateContext(deletionAuditEntries: entries);

            Assert.Equal(entries.Length, context.DeletionAuditEntries.Count);
            Assert.Same(entry, context.DeletionAuditEntries[0]);
        }

        [Fact]
        public void Client_ReturnsCorrectValue()
        {
            var client = new CollectorHttpClient();
            var context = CreateContext(client: client);

            Assert.Same(client, context.Client);
        }

        [Fact]
        public void CancellationToken_ReturnsCorrectValue()
        {
            var token = new CancellationToken(canceled: true);
            var context = CreateContext(token: token);

            Assert.Equal(token, context.CancellationToken);
        }

        [Fact]
        public void GetIndexV2Async_ReturnsMemoizedIndexTask()
        {
            var context = CreateContext();

            var task1 = context.GetIndexV2Async();
            var task2 = context.GetIndexV2Async();

            Assert.Same(task1, task2);
        }

        [Fact]
        public async Task GetIndexV2Async_ReturnsIndex()
        {
            var v2Resource = new Mock<IPackageRegistrationMetadataResource>();
            var expectedResult = Mock.Of<PackageRegistrationIndexMetadata>();

            v2Resource.Setup(x => x.GetIndexAsync(It.IsNotNull<PackageIdentity>(), It.IsNotNull<NuGet.Common.ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var context = CreateContext(v2Resource: v2Resource.Object);

            var actualResult = await context.GetIndexV2Async();

            Assert.Same(expectedResult, actualResult);
        }

        [Fact]
        public void GetIndexV3Async_ReturnsMemoizedIndexTask()
        {
            var context = CreateContext();

            var task1 = context.GetIndexV3Async();
            var task2 = context.GetIndexV3Async();

            Assert.Same(task1, task2);
        }

        [Fact]
        public async Task GetIndexV3Async_ReturnsIndex()
        {
            var v3Resource = new Mock<IPackageRegistrationMetadataResource>();
            var expectedResult = Mock.Of<PackageRegistrationIndexMetadata>();

            v3Resource.Setup(x => x.GetIndexAsync(It.IsNotNull<PackageIdentity>(), It.IsNotNull<NuGet.Common.ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var context = CreateContext(v3Resource: v3Resource.Object);

            var actualResult = await context.GetIndexV3Async();

            Assert.Same(expectedResult, actualResult);
        }

        [Fact]
        public void GetLeafV2Async_ReturnsMemoizedIndexTask()
        {
            var context = CreateContext();

            var task1 = context.GetLeafV2Async();
            var task2 = context.GetLeafV2Async();

            Assert.Same(task1, task2);
        }

        [Fact]
        public async Task GetLeafV2Async_ReturnsLeaf()
        {
            var v2Resource = new Mock<IPackageRegistrationMetadataResource>();
            var expectedResult = Mock.Of<PackageRegistrationLeafMetadata>();

            v2Resource.Setup(x => x.GetLeafAsync(It.IsNotNull<PackageIdentity>(), It.IsNotNull<NuGet.Common.ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var context = CreateContext(v2Resource: v2Resource.Object);

            var actualResult = await context.GetLeafV2Async();

            Assert.Same(expectedResult, actualResult);
        }

        [Fact]
        public void GetLeafV3Async_ReturnsMemoizedIndexTask()
        {
            var context = CreateContext();

            var task1 = context.GetLeafV3Async();
            var task2 = context.GetLeafV3Async();

            Assert.Same(task1, task2);
        }

        [Fact]
        public async Task GetLeafV3Async_ReturnsLeaf()
        {
            var v3Resource = new Mock<IPackageRegistrationMetadataResource>();
            var expectedResult = Mock.Of<PackageRegistrationLeafMetadata>();

            v3Resource.Setup(x => x.GetLeafAsync(It.IsNotNull<PackageIdentity>(), It.IsNotNull<NuGet.Common.ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var context = CreateContext(v3Resource: v3Resource.Object);

            var actualResult = await context.GetLeafV3Async();

            Assert.Same(expectedResult, actualResult);
        }

        [Fact]
        public async Task GetTimestampMetadataV2Async_ReturnsCorrectValue()
        {
            var timestampMetadataResourceV2 = new Mock<IPackageTimestampMetadataResource>();
            var timestampMetadata = new PackageTimestampMetadata();

            var context = CreateContext(timestampMetadataResource: timestampMetadataResourceV2.Object);

            timestampMetadataResourceV2.Setup(x => x.GetAsync(It.Is<ValidationContext>(vc => vc == context)))
                .ReturnsAsync(timestampMetadata);

            var actualResult = await context.GetTimestampMetadataV2Async();

            Assert.Same(timestampMetadata, actualResult);
        }

        [Fact]
        public void GetTimestampMetadataV2Async_ReturnsMemoizedTimestampMetadataV2Task()
        {
            var timestampMetadataResourceV2 = new Mock<IPackageTimestampMetadataResource>();
            var timestampMetadata = new PackageTimestampMetadata();

            var context = CreateContext(timestampMetadataResource: timestampMetadataResourceV2.Object);

            timestampMetadataResourceV2.Setup(x => x.GetAsync(It.Is<ValidationContext>(vc => vc == context)))
                .ReturnsAsync(timestampMetadata);

            var task1 = context.GetTimestampMetadataV2Async();
            var task2 = context.GetTimestampMetadataV2Async();

            Assert.Same(task1, task2);
        }

        private static ValidationContext CreateContext(
            PackageIdentity package = null,
            IEnumerable<CatalogIndexEntry> entries = null,
            IEnumerable<DeletionAuditEntry> deletionAuditEntries = null,
            Dictionary<FeedType, SourceRepository> feedToSource = null,
            CollectorHttpClient client = null,
            CancellationToken? token = null,
            ILogger<ValidationContext> logger = null,
            IPackageTimestampMetadataResource timestampMetadataResource = null,
            IPackageRegistrationMetadataResource v2Resource = null,
            IPackageRegistrationMetadataResource v3Resource = null)
        {
            if (feedToSource == null)
            {
                feedToSource = new Dictionary<FeedType, SourceRepository>();

                var v2Repository = new Mock<SourceRepository>();
                var v3Repository = new Mock<SourceRepository>();

                feedToSource.Add(FeedType.HttpV2, v2Repository.Object);
                feedToSource.Add(FeedType.HttpV3, v3Repository.Object);

                v2Repository.Setup(x => x.GetResource<IPackageTimestampMetadataResource>())
                    .Returns(timestampMetadataResource ?? Mock.Of<IPackageTimestampMetadataResource>());

                v2Repository.Setup(x => x.GetResource<IPackageRegistrationMetadataResource>())
                    .Returns(v2Resource ?? Mock.Of<IPackageRegistrationMetadataResource>());

                v3Repository.Setup(x => x.GetResource<IPackageRegistrationMetadataResource>())
                    .Returns(v3Resource ?? Mock.Of<IPackageRegistrationMetadataResource>());
            }

            var sourceRepositories = new ValidationSourceRepositories(
                feedToSource[FeedType.HttpV2],
                feedToSource[FeedType.HttpV3]);

            return new ValidationContext(
                package ?? _packageIdentity,
                entries ?? Enumerable.Empty<CatalogIndexEntry>(),
                deletionAuditEntries ?? Enumerable.Empty<DeletionAuditEntry>(),
                sourceRepositories,
                client ?? new CollectorHttpClient(),
                token ?? CancellationToken.None,
                logger ?? Mock.Of<ILogger<ValidationContext>>());
        }
    }
}
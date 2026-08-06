// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StagingValidationInputProviderFacts
    {
        [Fact]
        public async Task CopiesMatchingPackageArtifactToValidationSetPath()
        {
            var trackingId = Guid.NewGuid();
            var artifact = new StagedPackageArtifact
            {
                StagingEntry = new StagingEntry
                {
                    PackageKey = 42,
                    Package = new Package { PackageFileSize = 123 },
                },
                ValidationTrackingId = trackingId,
                BlobPath = "v1/2026/08/05/0123456789abcdef0123456789abcdef.nupkg",
                BlobETag = "\"etag\"",
                ContentHash = "hash",
            };
            var context = new Mock<IEntitiesContext>();
            context
                .SetupGet(x => x.StagedPackageArtifacts)
                .Returns(CreateDbSet(artifact).Object);
            var stagingBlobService = new Mock<IStagingBlobService>();
            var validationClient = new Mock<ICloudBlobClient>();
            var target = new StagingValidationInputProvider(
                context.Object,
                stagingBlobService.Object,
                validationClient.Object,
                new PackageFileMetadataService());
            var validationSet = new PackageValidationSet
            {
                PackageId = "Package",
                PackageNormalizedVersion = "1.0.0",
                PackageKey = 42,
                ValidationTrackingId = trackingId,
                ValidatingType = ValidatingType.Package,
            };

            await target.CopyStagedPackageForValidationSetAsync(validationSet);

            stagingBlobService.Verify(x => x.CopyAsync(
                It.Is<StagingBlobReference>(r =>
                    r.BlobPath == artifact.BlobPath
                    && r.ETag == artifact.BlobETag
                    && r.ContentHash == artifact.ContentHash
                    && r.ContentLength == 123
                    && r.BlobType == StagingBlobType.Nupkg),
                validationClient.Object,
                CoreConstants.Folders.ValidationFolderName,
                $"validation-sets/{trackingId}/package.1.0.0.nupkg",
                It.Is<IAccessCondition>(c => c.IfNoneMatchETag == "*")),
                Times.Once);
        }

        [Fact]
        public async Task CopiesMatchingSymbolArtifact()
        {
            var trackingId = Guid.NewGuid();
            var artifact = new StagedSymbolArtifact
            {
                SymbolPackageKey = 43,
                SymbolPackage = new SymbolPackage { FileSize = 456 },
                ValidationTrackingId = trackingId,
                BlobPath = "v1/2026/08/05/0123456789abcdef0123456789abcdef.snupkg",
                BlobETag = "\"etag\"",
                ContentHash = "hash",
            };
            var context = new Mock<IEntitiesContext>();
            context
                .SetupGet(x => x.StagedSymbolArtifacts)
                .Returns(CreateDbSet(artifact).Object);
            var stagingBlobService = new Mock<IStagingBlobService>();
            var validationClient = new Mock<ICloudBlobClient>();
            var target = new StagingValidationInputProvider(
                context.Object,
                stagingBlobService.Object,
                validationClient.Object,
                new SymbolPackageFileMetadataService());
            var validationSet = new PackageValidationSet
            {
                PackageId = "Package",
                PackageNormalizedVersion = "1.0.0",
                PackageKey = 43,
                ValidationTrackingId = trackingId,
                ValidatingType = ValidatingType.SymbolPackage,
            };

            await target.CopyStagedPackageForValidationSetAsync(validationSet);

            stagingBlobService.Verify(x => x.CopyAsync(
                It.Is<StagingBlobReference>(r =>
                    r.BlobPath == artifact.BlobPath
                    && r.ETag == artifact.BlobETag
                    && r.ContentHash == artifact.ContentHash
                    && r.ContentLength == 456
                    && r.BlobType == StagingBlobType.Snupkg),
                validationClient.Object,
                CoreConstants.Folders.ValidationFolderName,
                $"validation-sets/{trackingId}/package.1.0.0.snupkg",
                It.Is<IAccessCondition>(c => c.IfNoneMatchETag == "*")),
                Times.Once);
        }

        [Fact]
        public async Task RejectsArtifactWithDifferentTrackingId()
        {
            var artifact = new StagedPackageArtifact
            {
                StagingEntry = new StagingEntry { PackageKey = 42 },
                ValidationTrackingId = Guid.NewGuid(),
            };
            var context = new Mock<IEntitiesContext>();
            context
                .SetupGet(x => x.StagedPackageArtifacts)
                .Returns(CreateDbSet(artifact).Object);
            var stagingBlobService = new Mock<IStagingBlobService>();
            var target = new StagingValidationInputProvider(
                context.Object,
                stagingBlobService.Object,
                Mock.Of<ICloudBlobClient>(),
                new PackageFileMetadataService());
            var validationSet = new PackageValidationSet
            {
                PackageKey = 42,
                ValidationTrackingId = Guid.NewGuid(),
                ValidatingType = ValidatingType.Package,
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.CopyStagedPackageForValidationSetAsync(validationSet));

            stagingBlobService.Verify(
                x => x.CopyAsync(
                    It.IsAny<StagingBlobReference>(),
                    It.IsAny<ICloudBlobClient>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IAccessCondition>()),
                Times.Never);
        }

        private static Mock<DbSet<T>> CreateDbSet<T>(params T[] values) where T : class
        {
            var query = ((IEnumerable<T>)values).AsQueryable();
            var dbSet = new Mock<DbSet<T>>();
            dbSet.As<IQueryable<T>>().Setup(x => x.Provider).Returns(query.Provider);
            dbSet.As<IQueryable<T>>().Setup(x => x.Expression).Returns(query.Expression);
            dbSet.As<IQueryable<T>>().Setup(x => x.ElementType).Returns(query.ElementType);
            dbSet.As<IQueryable<T>>().Setup(x => x.GetEnumerator()).Returns(() => query.GetEnumerator());
            return dbSet;
        }
    }
}

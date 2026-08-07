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
                new PackageFileMetadataService(),
                Mock.Of<ICoreFileStorageService>());
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

        [Theory]
        [InlineData(PackageStatus.Available, CoreConstants.Folders.PackagesFolderName)]
        [InlineData(PackageStatus.Validating, CoreConstants.Folders.ValidationFolderName)]
        [InlineData(PackageStatus.FailedValidation, CoreConstants.Folders.ValidationFolderName)]
        public async Task CopiesMatchingSymbolArtifactAndOrdinaryParent(
            PackageStatus parentStatus,
            string parentSourceFolder)
        {
            var trackingId = Guid.NewGuid();
            var parentBytes = new byte[] { 1, 2, 3 };
            var parentHash = CryptographyService.GenerateHash(
                new System.IO.MemoryStream(parentBytes),
                CoreConstants.Sha512HashAlgorithmId);
            var artifact = new StagedSymbolArtifact
            {
                SymbolPackageKey = 43,
                SymbolPackage = new SymbolPackage { FileSize = 456 },
                StagingEntry = new StagingEntry
                {
                    PackageKey = 42,
                    Package = new Package
                    {
                        Key = 42,
                        Hash = parentHash,
                        PackageFileSize = parentBytes.Length,
                        PackageStatusKey = parentStatus,
                    },
                },
                ValidationTrackingId = trackingId,
                BlobPath = "v1/2026/08/05/0123456789abcdef0123456789abcdef.snupkg",
                BlobETag = "\"etag\"",
                ContentHash = "hash",
                ParentContentHash = parentHash,
            };
            var context = new Mock<IEntitiesContext>();
            context
                .SetupGet(x => x.StagedSymbolArtifacts)
                .Returns(CreateDbSet(artifact).Object);
            var stagingBlobService = new Mock<IStagingBlobService>();
            var validationClient = new Mock<ICloudBlobClient>();
            var fileStorageService = new Mock<ICoreFileStorageService>();
            fileStorageService
                .Setup(x => x.GetFileAsync(
                    CoreConstants.Folders.ValidationFolderName,
                    $"validation-sets/{trackingId}/parent/package.1.0.0.nupkg"))
                .ReturnsAsync(() => new System.IO.MemoryStream(parentBytes));
            var target = new StagingValidationInputProvider(
                context.Object,
                stagingBlobService.Object,
                validationClient.Object,
                new SymbolPackageFileMetadataService(),
                fileStorageService.Object);
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
            fileStorageService.Verify(x => x.CopyFileAsync(
                parentSourceFolder,
                "package.1.0.0.nupkg",
                CoreConstants.Folders.ValidationFolderName,
                $"validation-sets/{trackingId}/parent/package.1.0.0.nupkg",
                It.Is<IAccessCondition>(c => c.IfNoneMatchETag == "*")),
                Times.Once);
        }

        [Fact]
        public async Task CopiesStagedParentArtifactByExactHash()
        {
            var trackingId = Guid.NewGuid();
            var package = new Package
            {
                Key = 42,
                Hash = "parent-hash",
                PackageFileSize = 789,
                PackageStatusKey = PackageStatus.Staged,
            };
            var entry = new StagingEntry
            {
                PackageKey = package.Key,
                Package = package,
            };
            var symbolArtifact = new StagedSymbolArtifact
            {
                SymbolPackageKey = 43,
                SymbolPackage = new SymbolPackage { FileSize = 456 },
                StagingEntry = entry,
                ValidationTrackingId = trackingId,
                BlobPath = "symbol.snupkg",
                BlobETag = "\"symbol-etag\"",
                ContentHash = "symbol-hash",
                ParentContentHash = package.Hash,
            };
            var packageArtifact = new StagedPackageArtifact
            {
                StagingEntry = entry,
                BlobPath = "parent.nupkg",
                BlobETag = "\"parent-etag\"",
                ContentHash = package.Hash,
            };
            var context = new Mock<IEntitiesContext>();
            context.SetupGet(x => x.StagedSymbolArtifacts).Returns(CreateDbSet(symbolArtifact).Object);
            context.SetupGet(x => x.StagedPackageArtifacts).Returns(CreateDbSet(packageArtifact).Object);
            var stagingBlobService = new Mock<IStagingBlobService>();
            var validationClient = new Mock<ICloudBlobClient>();
            var target = new StagingValidationInputProvider(
                context.Object,
                stagingBlobService.Object,
                validationClient.Object,
                new SymbolPackageFileMetadataService(),
                Mock.Of<ICoreFileStorageService>());
            var validationSet = new PackageValidationSet
            {
                PackageId = "Package",
                PackageNormalizedVersion = "1.0.0",
                PackageKey = symbolArtifact.SymbolPackageKey,
                ValidationTrackingId = trackingId,
                ValidatingType = ValidatingType.SymbolPackage,
            };

            await target.CopyStagedPackageForValidationSetAsync(validationSet);

            stagingBlobService.Verify(x => x.CopyAsync(
                It.Is<StagingBlobReference>(r =>
                    r.BlobPath == packageArtifact.BlobPath
                    && r.ETag == packageArtifact.BlobETag
                    && r.ContentHash == package.Hash
                    && r.ContentLength == package.PackageFileSize
                    && r.BlobType == StagingBlobType.Nupkg),
                validationClient.Object,
                CoreConstants.Folders.ValidationFolderName,
                $"validation-sets/{trackingId}/parent/package.1.0.0.nupkg",
                It.Is<IAccessCondition>(c => c.IfNoneMatchETag == "*")),
                Times.Once);
        }

        [Theory]
        [InlineData(PackageStatus.Available, "replacement-parent-hash")]
        [InlineData(PackageStatus.Deleted, "original-parent-hash")]
        public async Task RejectsUnavailableOrChangedParent(
            PackageStatus parentStatus,
            string parentHash)
        {
            var trackingId = Guid.NewGuid();
            var symbolArtifact = new StagedSymbolArtifact
            {
                SymbolPackageKey = 43,
                SymbolPackage = new SymbolPackage { FileSize = 456 },
                StagingEntry = new StagingEntry
                {
                    PackageKey = 42,
                    Package = new Package
                    {
                        Hash = parentHash,
                        PackageStatusKey = parentStatus,
                    },
                },
                ValidationTrackingId = trackingId,
                BlobPath = "symbol.snupkg",
                BlobETag = "\"symbol-etag\"",
                ContentHash = "symbol-hash",
                ParentContentHash = "original-parent-hash",
            };
            var context = new Mock<IEntitiesContext>();
            context.SetupGet(x => x.StagedSymbolArtifacts).Returns(CreateDbSet(symbolArtifact).Object);
            var target = new StagingValidationInputProvider(
                context.Object,
                Mock.Of<IStagingBlobService>(),
                Mock.Of<ICloudBlobClient>(),
                new SymbolPackageFileMetadataService(),
                Mock.Of<ICoreFileStorageService>());
            var validationSet = new PackageValidationSet
            {
                PackageId = "Package",
                PackageNormalizedVersion = "1.0.0",
                PackageKey = symbolArtifact.SymbolPackageKey,
                ValidationTrackingId = trackingId,
                ValidatingType = ValidatingType.SymbolPackage,
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.CopyStagedPackageForValidationSetAsync(validationSet));
        }

        [Fact]
        public async Task DeletesParentSnapshotWhenCopiedContentDoesNotMatch()
        {
            var trackingId = Guid.NewGuid();
            var expectedBytes = new byte[] { 1 };
            var expectedHash = CryptographyService.GenerateHash(
                new System.IO.MemoryStream(expectedBytes),
                CoreConstants.Sha512HashAlgorithmId);
            var symbolArtifact = new StagedSymbolArtifact
            {
                SymbolPackageKey = 43,
                SymbolPackage = new SymbolPackage { FileSize = 456 },
                StagingEntry = new StagingEntry
                {
                    PackageKey = 42,
                    Package = new Package
                    {
                        Hash = expectedHash,
                        PackageFileSize = expectedBytes.Length,
                        PackageStatusKey = PackageStatus.Available,
                    },
                },
                ValidationTrackingId = trackingId,
                BlobPath = "symbol.snupkg",
                BlobETag = "\"symbol-etag\"",
                ContentHash = "symbol-hash",
                ParentContentHash = expectedHash,
            };
            var context = new Mock<IEntitiesContext>();
            context.SetupGet(x => x.StagedSymbolArtifacts).Returns(CreateDbSet(symbolArtifact).Object);
            var fileStorageService = new Mock<ICoreFileStorageService>();
            var parentPath = $"validation-sets/{trackingId}/parent/package.1.0.0.nupkg";
            fileStorageService
                .Setup(x => x.GetFileAsync(CoreConstants.Folders.ValidationFolderName, parentPath))
                .ReturnsAsync(new System.IO.MemoryStream(new byte[] { 2 }));
            var target = new StagingValidationInputProvider(
                context.Object,
                Mock.Of<IStagingBlobService>(),
                Mock.Of<ICloudBlobClient>(),
                new SymbolPackageFileMetadataService(),
                fileStorageService.Object);
            var validationSet = new PackageValidationSet
            {
                PackageId = "Package",
                PackageNormalizedVersion = "1.0.0",
                PackageKey = symbolArtifact.SymbolPackageKey,
                ValidationTrackingId = trackingId,
                ValidatingType = ValidatingType.SymbolPackage,
            };

            await Assert.ThrowsAsync<StagingBlobIntegrityException>(
                () => target.CopyStagedPackageForValidationSetAsync(validationSet));

            fileStorageService.Verify(
                x => x.DeleteFileAsync(CoreConstants.Folders.ValidationFolderName, parentPath),
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
                new PackageFileMetadataService(),
                Mock.Of<ICoreFileStorageService>());
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StagedPackageTerminalStateProcessorFacts
    {
        [Fact]
        public async Task FailureMarksMatchingArtifactAsValidationFailed()
        {
            var facts = new Facts();

            await facts.Target.ProcessAsync(
                facts.ValidationSet,
                facts.Package,
                StagingArtifactStatus.ValidationFailed);

            Assert.Equal(StagingArtifactStatus.ValidationFailed, facts.PackageArtifact.Status);
            Assert.Null(facts.PackageArtifact.ValidatedDate);
            facts.EntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
            facts.StagingBlobService.Verify(
                x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()),
                Times.Never);
        }

        [Fact]
        public async Task StaleOutcomeDoesNotChangeCurrentArtifact()
        {
            var facts = new Facts();
            facts.ValidationSet.ValidationTrackingId = Guid.NewGuid();

            await facts.Target.ProcessAsync(
                facts.ValidationSet,
                facts.Package,
                StagingArtifactStatus.Ready);

            Assert.Equal(StagingArtifactStatus.Validating, facts.PackageArtifact.Status);
            facts.EntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never);
            facts.StagingBlobService.Verify(
                x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()),
                Times.Never);
        }

        [Fact]
        public async Task SuccessWithoutProcessorRetainsBlobAndMarksArtifactReady()
        {
            var facts = new Facts();

            await facts.Target.ProcessAsync(
                facts.ValidationSet,
                facts.Package,
                StagingArtifactStatus.Ready);

            Assert.Equal(StagingArtifactStatus.Ready, facts.PackageArtifact.Status);
            Assert.NotNull(facts.PackageArtifact.ValidatedDate);
            Assert.Equal(Facts.OriginalBlobPath, facts.PackageArtifact.BlobPath);
            facts.StagingBlobService.Verify(
                x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()),
                Times.Never);
            facts.CorePackageService.Verify(
                x => x.UpdatePackageStreamMetadataAsync(
                    It.IsAny<Package>(),
                    It.IsAny<PackageStreamMetadata>(),
                    It.IsAny<bool>()),
                Times.Never);
            facts.StagingBlobCleanups.Verify(x => x.Add(It.IsAny<StagingBlobCleanup>()), Times.Never);
            facts.EntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ProcessorSuccessReplacesBlobAndPackageMetadataAtomically()
        {
            var facts = new Facts();
            var replacement = facts.EnableProcessor("new-hash", 456);
            PackageStreamMetadata metadata = null;
            facts.CorePackageService
                .Setup(x => x.UpdatePackageStreamMetadataAsync(facts.Package, It.IsAny<PackageStreamMetadata>(), false))
                .Callback<Package, PackageStreamMetadata, bool>((_, value, __) => metadata = value)
                .Returns(Task.CompletedTask);

            await facts.Target.ProcessAsync(
                facts.ValidationSet,
                facts.Package,
                StagingArtifactStatus.Ready);

            Assert.Equal(StagingArtifactStatus.Ready, facts.PackageArtifact.Status);
            Assert.Equal(replacement.BlobPath, facts.PackageArtifact.BlobPath);
            Assert.Equal(replacement.ETag, facts.PackageArtifact.BlobETag);
            Assert.Equal(replacement.ContentHash, facts.PackageArtifact.ContentHash);
            Assert.Equal(replacement.ContentHash, metadata.Hash);
            Assert.Equal(replacement.ContentLength, metadata.Size);
            Assert.Equal(CoreConstants.Sha512HashAlgorithmId, metadata.HashAlgorithm);
            facts.StagingBlobCleanups.Verify(x => x.Add(It.Is<StagingBlobCleanup>(c =>
                c.BlobPath == Facts.OriginalBlobPath
                && c.ExpectedETag == Facts.OriginalBlobETag)), Times.Once);
            facts.EntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ProcessorHashChangeRevalidatesRetainedSymbolBeforeCommit()
        {
            var facts = new Facts(includeSymbol: true);
            facts.EnableProcessor("new-hash", 456);
            PackageValidationMessageData message = null;
            var enqueuedBeforeCommit = false;
            facts.ValidationEnqueuer
                .Setup(x => x.SendMessageAsync(
                    It.IsAny<PackageValidationMessageData>(),
                    It.IsAny<DateTimeOffset>()))
                .Callback<PackageValidationMessageData, DateTimeOffset>((value, _) =>
                {
                    message = value;
                    enqueuedBeforeCommit = facts.EntitiesContext.Invocations.All(
                        i => i.Method.Name != nameof(IEntitiesContext.SaveChangesAsync));
                })
                .Returns(Task.CompletedTask);

            await facts.Target.ProcessAsync(
                facts.ValidationSet,
                facts.Package,
                StagingArtifactStatus.Ready);

            Assert.True(enqueuedBeforeCommit);
            Assert.Equal(StagingArtifactStatus.Validating, facts.SymbolArtifact.Status);
            Assert.Equal("new-hash", facts.SymbolArtifact.ParentContentHash);
            Assert.Null(facts.SymbolArtifact.ValidatedDate);
            Assert.Equal(facts.SymbolArtifact.ValidationTrackingId, message.ProcessValidationSet.ValidationTrackingId);
            Assert.Equal(ValidatingType.SymbolPackage, message.ProcessValidationSet.ValidatingType);
            Assert.Equal(facts.SymbolArtifact.SymbolPackageKey, message.ProcessValidationSet.EntityKey);
        }

        private class Facts
        {
            private readonly Mock<DbSet<StagedPackageArtifact>> _stagedPackageArtifacts;
            private readonly Mock<DbSet<StagedSymbolArtifact>> _stagedSymbolArtifacts;
            private readonly Mock<IValidationFileService> _packageFileService;
            private readonly Mock<IFileDownloader> _fileDownloader;
            private readonly Mock<IValidatorProvider> _validatorProvider;

            public Facts(bool includeSymbol = false)
            {
                Package = new Package
                {
                    Key = 42,
                    Hash = "old-hash",
                    PackageFileSize = 123,
                };
                var entry = new StagingEntry
                {
                    Key = 43,
                    PackageKey = Package.Key,
                    Package = Package,
                };
                PackageArtifact = new StagedPackageArtifact
                {
                    StagingEntryKey = entry.Key,
                    StagingEntry = entry,
                    BlobPath = OriginalBlobPath,
                    BlobETag = OriginalBlobETag,
                    ContentHash = Package.Hash,
                    Status = StagingArtifactStatus.Validating,
                    ValidationTrackingId = Guid.NewGuid(),
                };
                SymbolArtifact = includeSymbol
                    ? new StagedSymbolArtifact
                    {
                        StagingEntryKey = entry.Key,
                        StagingEntry = entry,
                        SymbolPackageKey = 44,
                        BlobPath = "v1/2026/08/05/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.snupkg",
                        BlobETag = "\"symbol-etag\"",
                        ContentHash = "symbol-hash",
                        ParentContentHash = Package.Hash,
                        Status = StagingArtifactStatus.Ready,
                        ValidationTrackingId = Guid.NewGuid(),
                        ValidatedDate = DateTime.UtcNow,
                    }
                    : null;
                ValidationSet = new PackageValidationSet
                {
                    PackageId = "Package",
                    PackageNormalizedVersion = "1.0.0",
                    PackageKey = Package.Key,
                    ValidationTrackingId = PackageArtifact.ValidationTrackingId,
                    ValidatingType = ValidatingType.Package,
                    PackageValidations = new List<PackageValidation>
                    {
                        new PackageValidation { Type = "validator" },
                    },
                };

                _stagedPackageArtifacts = CreateDbSet(PackageArtifact);
                _stagedSymbolArtifacts = CreateDbSet(
                    SymbolArtifact == null ? Array.Empty<StagedSymbolArtifact>() : new[] { SymbolArtifact });
                StagingBlobCleanups = CreateDbSet<StagingBlobCleanup>();
                EntitiesContext = new Mock<IEntitiesContext>();
                EntitiesContext.SetupGet(x => x.StagedPackageArtifacts).Returns(_stagedPackageArtifacts.Object);
                EntitiesContext.SetupGet(x => x.StagedSymbolArtifacts).Returns(_stagedSymbolArtifacts.Object);
                EntitiesContext.SetupGet(x => x.StagingBlobCleanups).Returns(StagingBlobCleanups.Object);
                EntitiesContext.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

                _packageFileService = new Mock<IValidationFileService>();
                _fileDownloader = new Mock<IFileDownloader>();
                StagingBlobService = new Mock<IStagingBlobService>();
                _validatorProvider = new Mock<IValidatorProvider>();
                CorePackageService = new Mock<ICorePackageService>();
                ValidationEnqueuer = new Mock<IPackageValidationEnqueuer>();
                var validationConfiguration = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
                validationConfiguration
                    .SetupGet(x => x.Value)
                    .Returns(new ValidationConfiguration
                    {
                        ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(1),
                    });

                Target = new StagedPackageTerminalStateProcessor(
                    EntitiesContext.Object,
                    _packageFileService.Object,
                    _fileDownloader.Object,
                    StagingBlobService.Object,
                    _validatorProvider.Object,
                    CorePackageService.Object,
                    ValidationEnqueuer.Object,
                    validationConfiguration.Object,
                    Mock.Of<ILogger<StagedPackageTerminalStateProcessor>>());
            }

            public const string OriginalBlobPath = "v1/2026/08/05/0123456789abcdef0123456789abcdef.nupkg";
            public const string OriginalBlobETag = "\"old-etag\"";

            public Package Package { get; }
            public StagedPackageArtifact PackageArtifact { get; }
            public StagedSymbolArtifact SymbolArtifact { get; }
            public PackageValidationSet ValidationSet { get; }
            public Mock<IEntitiesContext> EntitiesContext { get; }
            public Mock<DbSet<StagingBlobCleanup>> StagingBlobCleanups { get; }
            public Mock<IStagingBlobService> StagingBlobService { get; }
            public Mock<ICorePackageService> CorePackageService { get; }
            public Mock<IPackageValidationEnqueuer> ValidationEnqueuer { get; }
            public StagedPackageTerminalStateProcessor Target { get; }

            public StagingBlobReference EnableProcessor(string hash, long length)
            {
                var uri = new Uri("https://example.test/processed.nupkg");
                var content = new MemoryStream(Encoding.UTF8.GetBytes("processed"));
                var replacement = new StagingBlobReference(
                    "v1/2026/08/05/fedcba9876543210fedcba9876543210.nupkg",
                    "\"new-etag\"",
                    hash,
                    length,
                    StagingBlobType.Nupkg);

                _validatorProvider.Setup(x => x.IsNuGetProcessor("validator")).Returns(true);
                _packageFileService
                    .Setup(x => x.GetPackageForValidationSetReadUriAsync(
                        ValidationSet,
                        null,
                        It.IsAny<DateTimeOffset>()))
                    .ReturnsAsync(uri);
                _fileDownloader
                    .Setup(x => x.DownloadAsync(uri, CancellationToken.None))
                    .ReturnsAsync(FileDownloadResult.Ok(content));
                StagingBlobService
                    .Setup(x => x.CreateAsync(content, StagingBlobType.Nupkg))
                    .ReturnsAsync(replacement);
                CorePackageService
                    .Setup(x => x.UpdatePackageStreamMetadataAsync(
                        Package,
                        It.IsAny<PackageStreamMetadata>(),
                        false))
                    .Returns(Task.CompletedTask);

                return replacement;
            }
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

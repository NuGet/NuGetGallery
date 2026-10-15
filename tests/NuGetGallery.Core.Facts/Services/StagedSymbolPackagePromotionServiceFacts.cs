// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class StagedSymbolPackagePromotionServiceFacts
    {
        [Fact]
        public async Task PublishesImmutableSymbolsAndRetainsAttemptUntilOrchestrationCompletes()
        {
            var fixture = new Fixture();

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);
            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(PackageStatus.Available, fixture.Symbol.StatusKey);
            Assert.Equal(StagedPackageStatus.Succeeded, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            fixture.Blobs.Verify(service => service.GetPackageReadUriAsync("symbols/43", "etag"), Times.Once);
            fixture.Files.Verify(service => service.CopyFileAsync(Fixture.UploadUri, CoreConstants.Folders.SymbolPackagesFolderName,
                Fixture.FileName, It.Is<IAccessCondition>(condition => condition.IfNoneMatchETag == "*")), Times.Once);
            fixture.Attempts.Verify(repository => repository.DeleteOnCommit(It.IsAny<StagedSymbolPackage>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupRemovesOnlySuccessfulAttemptAndKeepsAnIdentityWithPrivateParent(bool hasPrivateParent)
        {
            var fixture = new Fixture();
            fixture.Attempt.Status = StagedPackageStatus.Succeeded;
            fixture.Identity.CurrentStagedPackageKey = hasPrivateParent ? 99 : (int?)null;

            await fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Null(fixture.Identity.CurrentStagedSymbolPackageKey);
            fixture.Attempts.Verify(repository => repository.DeleteOnCommit(fixture.Attempt), Times.Once);
            fixture.Identities.Verify(repository => repository.DeleteOnCommit(fixture.Identity), hasPrivateParent ? Times.Never() : Times.Once());
        }

        [Fact]
        public async Task FailureRetainsPrivateSymbolsAndDoesNotChangeParent()
        {
            var fixture = new Fixture();

            await fixture.Target.FailAsync(fixture.Attempt.Key, fixture.PromotionId);
            await fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Staged, fixture.Symbol.StatusKey);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            fixture.Attempts.Verify(repository => repository.DeleteOnCommit(It.IsAny<StagedSymbolPackage>()), Times.Never);
            fixture.Files.Verify(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()), Times.Never);
        }

        [Fact]
        public async Task StalePromotionCannotPublishFailOrRemoveCurrentAttempt()
        {
            var fixture = new Fixture();
            var oldPromotionId = Guid.NewGuid();

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, oldPromotionId);
            await fixture.Target.FailAsync(fixture.Attempt.Key, oldPromotionId);

            Assert.Equal(StagedPackageStatus.Promoting, fixture.Attempt.Status);
            fixture.Attempt.Status = StagedPackageStatus.Succeeded;
            await fixture.Target.CleanUpAsync(fixture.Attempt.Key, oldPromotionId);

            Assert.Equal(fixture.Attempt.Key, fixture.Identity.CurrentStagedSymbolPackageKey);
            fixture.Attempts.Verify(repository => repository.DeleteOnCommit(It.IsAny<StagedSymbolPackage>()), Times.Never);
            fixture.Attempts.Verify(repository => repository.CommitChangesAsync(), Times.Never);
            fixture.Files.Verify(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LostEligibilityDuringCopyFailsAndRemovesNewPublicBlob(bool loseOwnership)
        {
            var fixture = new Fixture();
            fixture.Files.Setup(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()))
                .Callback(() =>
                {
                    if (loseOwnership)
                    {
                        fixture.Parent.PackageRegistration.Owners.Clear();
                    }
                    else
                    {
                        fixture.Parent.PackageStatusKey = PackageStatus.Deleted;
                    }
                }).Returns(Task.CompletedTask);

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Staged, fixture.Symbol.StatusKey);
            fixture.Files.Verify(service => service.DeleteFileAsync(CoreConstants.Folders.SymbolPackagesFolderName, Fixture.FileName), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CommitFailureCompensatesOnlyIfPublicationWasNotPersisted(bool wasCommitted)
        {
            var fixture = new Fixture();
            fixture.Attempts.Setup(repository => repository.CommitChangesAsync()).Callback(() =>
            {
                if (wasCommitted)
                {
                    fixture.StoredSymbol.StatusKey = PackageStatus.Available;
                }
            }).ThrowsAsync(new InvalidOperationException("Database unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId));

            fixture.Files.Verify(service => service.DeleteFileAsync(CoreConstants.Folders.SymbolPackagesFolderName, Fixture.FileName),
                wasCommitted ? Times.Never() : Times.Once());
        }

        [Fact]
        public async Task IdenticalPublicCopyCompletesPromotionWithoutBlobMetadata()
        {
            var fixture = new Fixture();
            fixture.SetExistingPublicCopy(Fixture.SymbolContent);

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(PackageStatus.Available, fixture.Symbol.StatusKey);
            Assert.Equal(StagedPackageStatus.Succeeded, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            fixture.Files.Verify(service => service.GetFileAsync(CoreConstants.Folders.SymbolPackagesFolderName, Fixture.FileName), Times.Once);
            fixture.Files.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MatchedCopyIsNotDeletedWhenPublicationCommitFailsAfterAnInitialAbsenceCheck()
        {
            var fixture = new Fixture();
            fixture.SetExistingPublicCopy(Fixture.SymbolContent, existed: false);
            fixture.Attempts.Setup(repository => repository.CommitChangesAsync()).ThrowsAsync(new InvalidOperationException("Database unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId));

            Assert.Equal(PackageStatus.Staged, fixture.StoredSymbol.StatusKey);
            fixture.Files.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MissingPublicContentAfterAConflictLeavesPromotionRetryable()
        {
            var fixture = new Fixture();
            fixture.SetExistingPublicCopy(Fixture.SymbolContent);
            fixture.Files.Setup(service => service.GetFileAsync(CoreConstants.Folders.SymbolPackagesFolderName, Fixture.FileName)).ReturnsAsync((Stream)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId));

            Assert.Equal(StagedPackageStatus.Promoting, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Staged, fixture.Symbol.StatusKey);
            fixture.Attempts.Verify(repository => repository.CommitChangesAsync(), Times.Never);
            fixture.Files.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DifferentPublicContentFailsWithoutDeletingExistingContent(bool differentSize)
        {
            var fixture = new Fixture();
            var content = differentSize ? new byte[] { 1, 2, 3, 4 } : new byte[] { 1, 2, 4 };
            fixture.SetExistingPublicCopy(content);

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Staged, fixture.Symbol.StatusKey);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            fixture.Files.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DoesNotRetireOrReplaceExistingPublicSymbols()
        {
            var fixture = new Fixture();
            fixture.Symbols.Setup(repository => repository.GetAll())
                .Returns(new[] { new SymbolPackage { Key = 99, PackageKey = fixture.Parent.Key, StatusKey = PackageStatus.Available } }.AsQueryable());

            await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId));

            fixture.Files.Verify(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()), Times.Never);
        }

        private class Fixture
        {
            public const string FileName = "packagea.1.0.0.snupkg";

            public static readonly Uri UploadUri = new Uri("https://example.test/staged-symbols");

            public static readonly byte[] SymbolContent = new byte[] { 1, 2, 3 };

            public Fixture()
            {
                Parent = new Package
                {
                    Key = 42,
                    NormalizedVersion = "1.0.0",
                    PackageStatusKey = PackageStatus.Available,
                    PackageRegistration = new PackageRegistration { Id = "PackageA", Owners = new[] { new User { Key = 7 } }.ToList() },
                };
                Identity = new StagedPackageIdentity { Key = Parent.Key, Package = Parent, OwnerKey = 7, CurrentStagedSymbolPackageKey = 43 };
                Symbol = new SymbolPackage { Key = 44, PackageKey = Parent.Key, Package = Parent, StatusKey = PackageStatus.Staged };
                Symbol.FileSize = SymbolContent.LongLength;
                Symbol.HashAlgorithm = CoreConstants.Sha512HashAlgorithmId;
                using (var content = new MemoryStream(SymbolContent))
                {
                    Symbol.Hash = CryptographyService.GenerateHash(content, Symbol.HashAlgorithm);
                }
                Attempt = new StagedSymbolPackage
                {
                    Key = 43,
                    StagedPackageIdentity = Identity,
                    SymbolPackageKey = Symbol.Key,
                    SymbolPackage = Symbol,
                    Status = StagedPackageStatus.Promoting,
                    ActivePromotionId = PromotionId,
                    UploadedBlobPath = "symbols/43",
                    UploadedBlobETag = "etag",
                };
                StoredSymbol = new SymbolPackage { Key = Symbol.Key, PackageKey = Parent.Key, StatusKey = PackageStatus.Staged };
                Attempts.Setup(repository => repository.GetAll()).Returns(new[] { Attempt }.AsQueryable());
                Attempts.Setup(repository => repository.ExecuteInTransactionAsync(It.IsAny<Func<Task>>())).Returns((Func<Task> action) => action());
                Attempts.Setup(repository => repository.CommitChangesAsync()).Callback(() => StoredSymbol.StatusKey = Symbol.StatusKey).Returns(Task.CompletedTask);
                Symbols.Setup(repository => repository.GetAll()).Returns(new[] { StoredSymbol }.AsQueryable());
                var symbolService = new Mock<ICoreSymbolPackageService>();
                symbolService.Setup(service => service.UpdateStatusAsync(Symbol, It.IsAny<PackageStatus>(), false))
                    .Callback<SymbolPackage, PackageStatus, bool>((symbol, status, commit) => symbol.StatusKey = status).Returns(Task.CompletedTask);
                Blobs.Setup(service => service.GetPackageReadUriAsync("symbols/43", "etag")).ReturnsAsync(UploadUri);
                Target = new StagedSymbolPackagePromotionService(Attempts.Object, Identities.Object, Symbols.Object, symbolService.Object,
                    Blobs.Object, Files.Object, Mock.Of<ILogger<StagedSymbolPackagePromotionService>>());
            }

            public Guid PromotionId { get; } = Guid.NewGuid();

            public Package Parent { get; }

            public StagedPackageIdentity Identity { get; }

            public SymbolPackage Symbol { get; }

            public SymbolPackage StoredSymbol { get; }

            public StagedSymbolPackage Attempt { get; }

            public Mock<IEntityRepository<StagedSymbolPackage>> Attempts { get; } = new Mock<IEntityRepository<StagedSymbolPackage>>();

            public Mock<IEntityRepository<StagedPackageIdentity>> Identities { get; } = new Mock<IEntityRepository<StagedPackageIdentity>>();

            public Mock<IEntityRepository<SymbolPackage>> Symbols { get; } = new Mock<IEntityRepository<SymbolPackage>>();

            public Mock<IStagingBlobService> Blobs { get; } = new Mock<IStagingBlobService>();

            public Mock<ICoreFileStorageService> Files { get; } = new Mock<ICoreFileStorageService>();

            public StagedSymbolPackagePromotionService Target { get; }

            public void SetExistingPublicCopy(byte[] content, bool existed = true)
            {
                Files.Setup(service => service.FileExistsAsync(CoreConstants.Folders.SymbolPackagesFolderName, FileName)).ReturnsAsync(existed);
                Files.Setup(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()))
                    .ThrowsAsync(new FileAlreadyExistsException());
                Files.Setup(service => service.GetFileAsync(CoreConstants.Folders.SymbolPackagesFolderName, FileName))
                    .ReturnsAsync(() => new MemoryStream(content));
            }
        }
    }
}

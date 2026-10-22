// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
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
        [Theory]
        [InlineData("owner-locked")]
        [InlineData("owner-unconfirmed")]
        [InlineData("registration-locked")]
        public async Task PublishingRestrictionsAtStartupFailSymbolsWithoutChangingTheParent(string restriction)
        {
            foreach (var grouped in new[] { false, true })
            {
                var fixture = new Fixture();
                if (grouped)
                {
                    fixture.AddToGroup();
                }

                switch (restriction)
                {
                    case "owner-locked":
                        fixture.Identity.Owner.UserStatusKey = UserStatus.Locked;
                        break;
                    case "owner-unconfirmed":
                        fixture.Identity.Owner.EmailAddress = null;
                        break;
                    case "registration-locked":
                        fixture.Parent.PackageRegistration.IsLocked = true;
                        break;
                    default:
                        throw new ArgumentException("Unknown publishing restriction.", nameof(restriction));
                }

                await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);
                await fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId);

                Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
                Assert.Equal(PackageStatus.Staged, fixture.Symbol.StatusKey);
                Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
                Assert.Equal(fixture.Attempt.Key, fixture.Identity.CurrentStagedSymbolPackageKey);
                fixture.Files.Verify(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()), Times.Never);
                fixture.Files.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                fixture.Groups.Verify(service => service.TryFinalizeAsync(It.IsAny<int>(), fixture.PromotionId), grouped ? Times.Once() : Times.Never());
            }
        }

        [Fact]
        public async Task OwnerLockedDuringCopyDoesNotInterruptSymbolPublication()
        {
            var fixture = new Fixture();
            fixture.AddToGroup();
            fixture.Files.Setup(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()))
                .Callback(() => fixture.Identity.Owner.UserStatusKey = UserStatus.Locked).Returns(Task.CompletedTask);

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);
            await fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.Succeeded, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Available, fixture.Symbol.StatusKey);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            fixture.Files.Verify(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()), Times.Once);
            fixture.Files.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            fixture.Groups.Verify(service => service.TryFinalizeAsync(It.IsAny<int>(), fixture.PromotionId), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PublishesImmutableSymbolsAndRetainsAttemptUntilOrchestrationCompletes(bool grouped)
        {
            var fixture = new Fixture();
            if (grouped)
            {
                fixture.AddToGroup();
            }

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);
            fixture.Identity.Owner.UserStatusKey = UserStatus.Locked;
            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(PackageStatus.Available, fixture.Symbol.StatusKey);
            Assert.Equal(grouped ? StagedPackageStatus.Promoting : StagedPackageStatus.Succeeded, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            fixture.Blobs.Verify(service => service.GetPackageReadUriAsync("symbols/43", "etag"), Times.Once);
            fixture.Files.Verify(service => service.CopyFileAsync(Fixture.UploadUri, CoreConstants.Folders.SymbolPackagesFolderName,
                Fixture.FileName, It.Is<IAccessCondition>(condition => condition.IfNoneMatchETag == "*")), Times.Once);
            fixture.Attempts.Verify(repository => repository.DeleteOnCommit(It.IsAny<StagedSymbolPackage>()), Times.Never);

            if (grouped)
            {
                fixture.Groups.Verify(service => service.TryFinalizeAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
                await fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId);

                Assert.Equal(StagedPackageStatus.Succeeded, fixture.Attempt.Status);
                Assert.Equal(fixture.Attempt.Key, fixture.Identity.CurrentStagedSymbolPackageKey);
                fixture.Groups.Verify(service => service.TryFinalizeAsync(fixture.Identity.StagingGroupKey.Value, fixture.PromotionId), Times.Once);
            }
        }

        [Fact]
        public async Task GroupedIngestionFailureBecomesTerminalOnlyAtOrchestrationCleanup()
        {
            var fixture = new Fixture();
            fixture.AddToGroup();

            await fixture.Target.FailAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.Promoting, fixture.Attempt.Status);
            fixture.Groups.Verify(service => service.TryFinalizeAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
            await fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            Assert.Equal(PackageStatus.Staged, fixture.Symbol.StatusKey);
            fixture.Attempts.Verify(repository => repository.DeleteOnCommit(It.IsAny<StagedSymbolPackage>()), Times.Never);
            fixture.Groups.Verify(service => service.TryFinalizeAsync(fixture.Identity.StagingGroupKey.Value, fixture.PromotionId), Times.Once);
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
            fixture.BlobCleanup.Verify(service => service.QueueSymbolFiles(fixture.Identity.Key), Times.Once);
            fixture.BlobCleanup.Verify(service => service.QueuePackageFiles(fixture.Identity.Key), hasPrivateParent ? Times.Never() : Times.Once());
        }

        [Fact]
        public async Task FailureRetainsPrivateSymbolsAndDoesNotChangeParent()
        {
            var fixture = new Fixture();
            fixture.AddPublicSymbols();

            await fixture.Target.FailAsync(fixture.Attempt.Key, fixture.PromotionId);
            await fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Staged, fixture.Symbol.StatusKey);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            Assert.Equal(PackageStatus.Available, fixture.StoredPreviousSymbol.StatusKey);
            Assert.Equal(Fixture.PreviousContent, fixture.PublicContent);
            fixture.BlobCleanup.Verify(service => service.QueuePackageFiles(It.IsAny<int>()), Times.Never);
            fixture.BlobCleanup.Verify(service => service.QueueSymbolFiles(It.IsAny<int>()), Times.Never);
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
        [InlineData(true, true)]
        public async Task LostEligibilityDuringCopyFailsAndRemovesNewPublicBlob(bool loseOwnership, bool hasPublicSymbols = false)
        {
            var fixture = new Fixture();
            Action loseEligibility = () =>
            {
                if (loseOwnership)
                {
                    fixture.Parent.PackageRegistration.Owners.Clear();
                }
                else
                {
                    fixture.Parent.PackageStatusKey = PackageStatus.Deleted;
                }
            };
            if (hasPublicSymbols)
            {
                fixture.AddPublicSymbols();
                fixture.AfterReplacement = loseEligibility;
            }
            else
            {
                fixture.Files.Setup(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()))
                    .Callback(loseEligibility).Returns(Task.CompletedTask);
            }

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Staged, fixture.Symbol.StatusKey);
            if (hasPublicSymbols)
            {
                Assert.Equal(PackageStatus.Available, fixture.StoredPreviousSymbol.StatusKey);
                Assert.Null(fixture.PublicContent);
            }
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReplacesPublishedSymbolsAndRecoversAnInterruptedFileSwap(bool interrupted)
        {
            var fixture = new Fixture();
            fixture.AddPublicSymbols();
            if (interrupted)
            {
                fixture.PublicContent = Fixture.SymbolContent;
            }

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);
            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(PackageStatus.Available, fixture.StoredSymbol.StatusKey);
            Assert.Equal(PackageStatus.Deleted, fixture.StoredPreviousSymbol.StatusKey);
            Assert.Equal(StagedPackageStatus.Succeeded, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            Assert.Equal(Fixture.SymbolContent, fixture.PublicContent);
            Assert.Equal(interrupted ? 0 : 1, fixture.ReplacementWrites);
            fixture.Blobs.Verify(service => service.GetPackageReadUriAsync("symbols/43", "etag"), interrupted ? Times.Never() : Times.Once());
            fixture.SymbolService.Verify(service => service.UpdateStatusAsync(fixture.PreviousSymbol, PackageStatus.Deleted, false), Times.Once);
        }

        [Fact]
        public async Task DifferentPublicContentCannotReplacePublishedSymbols()
        {
            var fixture = new Fixture();
            fixture.AddPublicSymbols();
            fixture.PublicContent = new byte[] { 7, 8, 9 };
            var publicContent = fixture.PublicContent;

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(PackageStatus.Staged, fixture.StoredSymbol.StatusKey);
            Assert.Equal(PackageStatus.Available, fixture.StoredPreviousSymbol.StatusKey);
            Assert.Equal(publicContent, fixture.PublicContent);
            Assert.Equal(0, fixture.ReplacementWrites);
        }

        [Fact]
        public async Task IdenticalPublishedSymbolsAreNotDeletedWhenTheCommitFails()
        {
            var fixture = new Fixture();
            fixture.AddPublicSymbols(identical: true);
            fixture.Attempts.Setup(repository => repository.CommitChangesAsync()).ThrowsAsync(new InvalidOperationException("Database unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId));

            Assert.Equal(Fixture.SymbolContent, fixture.PublicContent);
            Assert.Equal(PackageStatus.Available, fixture.StoredPreviousSymbol.StatusKey);
            Assert.Equal(0, fixture.ReplacementWrites);
            fixture.Files.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ReplacementRetryRecreatesTheFileRemovedByCommitFailureCompensation()
        {
            var fixture = new Fixture();
            fixture.AddPublicSymbols();
            fixture.Attempts.Setup(repository => repository.CommitChangesAsync()).ThrowsAsync(new InvalidOperationException("Database unavailable"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId));
            Assert.Null(fixture.PublicContent);
            Assert.Equal(PackageStatus.Staged, fixture.StoredSymbol.StatusKey);
            Assert.Equal(PackageStatus.Available, fixture.StoredPreviousSymbol.StatusKey);
            Assert.Equal(1, fixture.ReplacementWrites);
            fixture.Files.Verify(service => service.DeleteFileAsync(CoreConstants.Folders.SymbolPackagesFolderName, Fixture.FileName), Times.Once);

            // Reload the uncommitted Gallery state for the next delivery.
            fixture.Attempt.Status = StagedPackageStatus.Promoting;
            fixture.Symbol.StatusKey = PackageStatus.Staged;
            fixture.PreviousSymbol.StatusKey = PackageStatus.Available;
            fixture.Attempts.Setup(repository => repository.CommitChangesAsync()).Callback(() =>
            {
                fixture.StoredSymbol.StatusKey = fixture.Symbol.StatusKey;
                fixture.StoredPreviousSymbol.StatusKey = fixture.PreviousSymbol.StatusKey;
            }).Returns(Task.CompletedTask);

            await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);

            Assert.Equal(StagedPackageStatus.Succeeded, fixture.Attempt.Status);
            Assert.Equal(Fixture.SymbolContent, fixture.PublicContent);
            Assert.Equal(PackageStatus.Deleted, fixture.StoredPreviousSymbol.StatusKey);
            Assert.Equal(PackageStatus.Available, fixture.StoredSymbol.StatusKey);
            Assert.Equal(2, fixture.ReplacementWrites);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public async Task NotifiesOnlyTheSymbolResultAfterTerminalCommitAndNotAgainOnReplay(bool grouped, bool succeeded, bool finalizationFails = false)
        {
            var fixture = new Fixture();
            if (grouped)
            {
                fixture.AddToGroup();
            }

            var attempts = new List<StagedSymbolPackage> { fixture.Attempt };
            fixture.Attempts.Setup(repository => repository.GetAll()).Returns(attempts.AsQueryable());
            fixture.Attempts.Setup(repository => repository.DeleteOnCommit(fixture.Attempt)).Callback(() => attempts.Remove(fixture.Attempt));
            if (succeeded)
            {
                await fixture.Target.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId);
            }
            else
            {
                await fixture.Target.FailAsync(fixture.Attempt.Key, fixture.PromotionId);
            }

            fixture.Notifications.Verify(service => service.SendAsync(It.IsAny<User>(), It.IsAny<StagingPromotionArtifact>()), Times.Never);
            var completionCommitted = false;
            fixture.Attempts.Setup(repository => repository.CommitChangesAsync()).Callback(() => completionCommitted = true).Returns(Task.CompletedTask);
            fixture.Notifications.Setup(service => service.SendAsync(It.IsAny<User>(), It.IsAny<StagingPromotionArtifact>()))
                .Callback(() => Assert.True(completionCommitted)).Returns(Task.CompletedTask);

            if (finalizationFails)
            {
                fixture.Groups.SetupSequence(service => service.TryFinalizeAsync(fixture.Identity.StagingGroupKey.Value, fixture.PromotionId))
                    .ThrowsAsync(new TimeoutException())
                    .Returns(Task.CompletedTask);

                await Assert.ThrowsAsync<TimeoutException>(() => fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId));

                fixture.Notifications.Verify(service => service.SendAsync(fixture.Identity.Owner,
                    It.Is<StagingPromotionArtifact>(artifact => artifact.Symbols && artifact.Succeeded == succeeded)), Times.Once);
            }
            else
            {
                await fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId);
            }

            await fixture.Target.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId);

            fixture.Notifications.Verify(service => service.SendAsync(fixture.Identity.Owner,
                It.Is<StagingPromotionArtifact>(artifact => artifact.Symbols && artifact.Succeeded == succeeded
                    && artifact.PackageId == "PackageA" && artifact.Version == "1.0.0")), Times.Once);
            Assert.Equal(PackageStatus.Available, fixture.Parent.PackageStatusKey);
            if (grouped)
            {
                fixture.Groups.Verify(service => service.TryFinalizeAsync(fixture.Identity.StagingGroupKey.Value, fixture.PromotionId), Times.Exactly(2));
            }

            if (!grouped && !succeeded)
            {
                Assert.Null(fixture.Attempt.ActivePromotionId);
                Assert.Equal(fixture.Attempt.Key, fixture.Identity.CurrentStagedSymbolPackageKey);
            }
        }

        private class Fixture
        {
            public const string FileName = "packagea.1.0.0.snupkg";

            public static readonly Uri UploadUri = new Uri("https://example.test/staged-symbols");

            public static readonly byte[] SymbolContent = new byte[] { 1, 2, 3 };

            public static readonly byte[] PreviousContent = new byte[] { 4, 5, 6 };

            public Fixture()
            {
                Parent = new Package
                {
                    Key = 42,
                    NormalizedVersion = "1.0.0",
                    PackageStatusKey = PackageStatus.Available,
                    PackageRegistration = new PackageRegistration { Id = "PackageA", Owners = new[] { new User { Key = 7, EmailAddress = "owner@example.test" } }.ToList() },
                };
                Identity = new StagedPackageIdentity
                {
                    Key = Parent.Key,
                    Package = Parent,
                    OwnerKey = 7,
                    Owner = Parent.PackageRegistration.Owners.Single(),
                    CurrentStagedSymbolPackageKey = 43,
                };
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
                Attempts.Setup(repository => repository.CommitChangesAsync()).Callback(() =>
                {
                    StoredSymbol.StatusKey = Symbol.StatusKey;
                    if (PreviousSymbol != null)
                    {
                        StoredPreviousSymbol.StatusKey = PreviousSymbol.StatusKey;
                    }
                }).Returns(Task.CompletedTask);
                Symbols.Setup(repository => repository.GetAll()).Returns(new[] { StoredSymbol }.AsQueryable());
                SymbolService.Setup(service => service.UpdateStatusAsync(It.IsAny<SymbolPackage>(), It.IsAny<PackageStatus>(), false))
                    .Callback<SymbolPackage, PackageStatus, bool>((symbol, status, commit) => symbol.StatusKey = status).Returns(Task.CompletedTask);
                Blobs.Setup(service => service.GetPackageReadUriAsync("symbols/43", "etag")).ReturnsAsync(UploadUri);
                Target = new StagedSymbolPackagePromotionService(Attempts.Object, Identities.Object, Symbols.Object, SymbolService.Object,
                    Blobs.Object, Files.Object, Groups.Object, BlobCleanup.Object, Notifications.Object, Mock.Of<ILogger<StagedSymbolPackagePromotionService>>());
            }

            public Guid PromotionId { get; } = Guid.NewGuid();

            public Mock<IStagingBlobCleanupService> BlobCleanup { get; } = new Mock<IStagingBlobCleanupService>();

            public Mock<IStagingPromotionNotificationService> Notifications { get; } = new Mock<IStagingPromotionNotificationService>();

            public Package Parent { get; }

            public StagedPackageIdentity Identity { get; }

            public SymbolPackage Symbol { get; }

            public SymbolPackage StoredSymbol { get; }

            public StagedSymbolPackage Attempt { get; }

            public SymbolPackage PreviousSymbol { get; private set; }

            public SymbolPackage StoredPreviousSymbol { get; private set; }

            public byte[] PublicContent { get; set; }

            public string PublicETag { get; set; } = "public-etag";

            public int ReplacementWrites { get; private set; }

            public Action AfterReplacement { get; set; }

            public Mock<ICoreSymbolPackageService> SymbolService { get; } = new Mock<ICoreSymbolPackageService>();

            public Mock<IEntityRepository<StagedSymbolPackage>> Attempts { get; } = new Mock<IEntityRepository<StagedSymbolPackage>>();

            public Mock<IEntityRepository<StagedPackageIdentity>> Identities { get; } = new Mock<IEntityRepository<StagedPackageIdentity>>();

            public Mock<IEntityRepository<SymbolPackage>> Symbols { get; } = new Mock<IEntityRepository<SymbolPackage>>();

            public Mock<IStagingBlobService> Blobs { get; } = new Mock<IStagingBlobService>();

            public Mock<ICoreFileStorageService> Files { get; } = new Mock<ICoreFileStorageService>();

            public Mock<IStagingGroupPromotionService> Groups { get; } = new Mock<IStagingGroupPromotionService>();

            public StagedSymbolPackagePromotionService Target { get; }

            public void AddToGroup()
            {
                Identity.StagingGroupKey = 7;
                Identity.StagingGroup = new StagingGroup { Key = 7, ActivePromotionId = PromotionId };
            }

            public void AddPublicSymbols(bool identical = false)
            {
                var previousContent = identical ? SymbolContent : PreviousContent;
                PreviousSymbol = new SymbolPackage { Key = 99, PackageKey = Parent.Key, StatusKey = PackageStatus.Available, FileSize = previousContent.Length, HashAlgorithm = CoreConstants.Sha512HashAlgorithmId };
                using (var content = new MemoryStream(previousContent))
                {
                    PreviousSymbol.Hash = CryptographyService.GenerateHash(content, PreviousSymbol.HashAlgorithm);
                }
                StoredPreviousSymbol = new SymbolPackage { Key = PreviousSymbol.Key, PackageKey = Parent.Key, StatusKey = PackageStatus.Available };
                var tracked = CreateQuery(new[] { PreviousSymbol, Symbol }.AsQueryable());
                var persisted = CreateQuery(new[] { StoredPreviousSymbol, StoredSymbol }.AsQueryable());
                tracked.Setup(query => query.AsNoTracking()).Returns(persisted.Object);
                Symbols.Setup(repository => repository.GetAll()).Returns(tracked.Object);
                PublicContent = previousContent;
                Files.Setup(service => service.FileExistsAsync(CoreConstants.Folders.SymbolPackagesFolderName, FileName)).ReturnsAsync(() => PublicContent != null);
                Files.Setup(service => service.GetFileReferenceAsync(CoreConstants.Folders.SymbolPackagesFolderName, FileName, null))
                    .ReturnsAsync(() => PublicContent == null ? null : CloudFileReference.Modified(new MemoryStream(PublicContent), PublicETag));
                Files.Setup(service => service.GetFileAsync(CoreConstants.Folders.SymbolPackagesFolderName, FileName))
                    .ReturnsAsync(() => PublicContent == null ? null : new MemoryStream(PublicContent));
                Files.Setup(service => service.CopyFileAsync(UploadUri, CoreConstants.Folders.SymbolPackagesFolderName, FileName, It.IsAny<IAccessCondition>()))
                    .Callback<Uri, string, string, IAccessCondition>((uri, folder, name, condition) =>
                    {
                        if (PublicContent == null)
                        {
                            Assert.Equal("*", condition.IfNoneMatchETag);
                        }
                        else
                        {
                            Assert.Equal(PublicETag, condition.IfMatchETag);
                        }
                        PublicContent = SymbolContent;
                        ReplacementWrites++;
                        PublicETag = "public-etag-" + ReplacementWrites;
                        if (ReplacementWrites == 1)
                        {
                            AfterReplacement?.Invoke();
                        }
                    }).Returns(Task.CompletedTask);
                Files.Setup(service => service.DeleteFileAsync(CoreConstants.Folders.SymbolPackagesFolderName, FileName))
                    .Callback(() => PublicContent = null).Returns(Task.CompletedTask);
            }

            private static Mock<DbSet<SymbolPackage>> CreateQuery(IQueryable<SymbolPackage> symbols)
            {
                var query = new Mock<DbSet<SymbolPackage>>();
                query.As<IQueryable<SymbolPackage>>().Setup(set => set.Provider).Returns(symbols.Provider);
                query.As<IQueryable<SymbolPackage>>().Setup(set => set.Expression).Returns(symbols.Expression);
                query.As<IQueryable<SymbolPackage>>().Setup(set => set.ElementType).Returns(symbols.ElementType);
                query.As<IQueryable<SymbolPackage>>().Setup(set => set.GetEnumerator()).Returns(() => symbols.GetEnumerator());
                return query;
            }

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

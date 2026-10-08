// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Staging.Promotion.Tests
{
    public class StagedPackagePromotionMessageHandlerFacts
    {
        [Theory]
        [InlineData("owner-locked")]
        [InlineData("owner-unconfirmed")]
        [InlineData("registration-locked")]
        public async Task PublishingRestrictionsAtStartupFailParentAndPairedSymbols(string restriction)
        {
            foreach (var grouped in new[] { false, true })
            {
                var context = new TestContext();
                var symbols = context.AddAcceptedSymbols();
                if (grouped)
                {
                    context.AddToGroup();
                }

                switch (restriction)
                {
                    case "owner-locked":
                        context.StagedPackageIdentity.Owner.UserStatusKey = UserStatus.Locked;
                        break;
                    case "owner-unconfirmed":
                        context.StagedPackageIdentity.Owner.EmailAddress = null;
                        break;
                    case "registration-locked":
                        context.Package.PackageRegistration.IsLocked = true;
                        break;
                    default:
                        throw new ArgumentException("Unknown publishing restriction.", nameof(restriction));
                }

                Assert.True(await context.Target.HandleAsync(context.Message));

                Assert.Equal(PackageStatus.Staged, context.Package.PackageStatusKey);
                Assert.Equal(StagedPackageStatus.PromotionFailed, context.StagedPackage.Status);
                Assert.Equal(StagedPackageStatus.PromotionFailed, symbols.Status);
                Assert.Contains(context.StagedPackage, context.StagedPackages);
                context.MessageEnqueuer.Verify(service => service.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
                context.Notifications.Verify(service => service.SendAsync(context.StagedPackageIdentity.Owner,
                    It.Is<StagingPromotionArtifact>(artifact => !artifact.Succeeded)), Times.Exactly(2));
                context.PackageFileStorageService.Verify(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()), Times.Never);
                context.PackageFileStorageService.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                context.PackageService.Verify(service => service.UpdatePackageStatusAsync(It.IsAny<Package>(), PackageStatus.Available, false), Times.Never);
                context.StagingGroupPromotionService.Verify(service => service.TryFinalizeAsync(It.IsAny<int>(), context.PromotionId), grouped ? Times.Once() : Times.Never());
            }
        }

        [Fact]
        public async Task OwnerLockedDuringCopyDoesNotInterruptParentPublication()
        {
            var context = new TestContext();
            var symbols = context.AddAcceptedSymbols();
            context.AddToGroup();
            context.PackageFileStorageService.Setup(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()))
                .Callback(() => context.StagedPackageIdentity.Owner.UserStatusKey = UserStatus.Locked).Returns(Task.CompletedTask);

            Assert.True(await context.Target.HandleAsync(context.Message));

            Assert.Equal(PackageStatus.Available, context.Package.PackageStatusKey);
            Assert.Equal(StagedPackageStatus.Succeeded, context.StagedPackage.Status);
            Assert.Equal(StagedPackageStatus.Promoting, symbols.Status);
            context.PackageFileStorageService.Verify(service => service.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()), Times.Once);
            context.PackageFileStorageService.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            context.MessageEnqueuer.Verify(service => service.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Once);
            context.StagingGroupPromotionService.Verify(service => service.TryFinalizeAsync(It.IsAny<int>(), context.PromotionId), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PublishesExactValidatedPackageAndRemovesStagingRow(bool hasStagedSymbols)
        {
            var context = new TestContext();
            if (hasStagedSymbols)
            {
                context.StagedPackageIdentity.CurrentStagedSymbolPackageKey = 100;
            }

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Equal(PackageStatus.Available, context.Package.PackageStatusKey);
            Assert.Equal(CoreConstants.Sha512HashAlgorithmId, context.Package.HashAlgorithm);
            Assert.Equal(context.Content.Length, context.Package.PackageFileSize);
            using (var sha512 = SHA512.Create())
            {
                Assert.Equal(
                    Convert.ToBase64String(sha512.ComputeHash(context.Content)),
                    context.Package.Hash);
            }
            context.StagingBlobService.Verify(
                x => x.OpenPackageFileAsync(
                    context.StagedPackage.ValidatedBlobPath,
                    context.StagedPackage.ValidatedBlobETag),
                Times.Once);
            context.PackageFileStorageService.Verify(
                x => x.CopyFileAsync(
                    context.SourceUri,
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg",
                    It.Is<IAccessCondition>(condition => condition.IfNoneMatchETag == "*")),
                Times.Once);
            Assert.Equal(CoreConstants.DefaultCacheControl, context.PublicBlobProperties.Object.CacheControl);
            context.PackageService.Verify(
                x => x.UpdatePackageStreamMetadataAsync(
                    context.Package,
                    It.IsAny<NuGetGallery.Packaging.PackageStreamMetadata>(),
                    false),
                Times.Once);
            context.PackageService.Verify(
                x => x.UpdatePackageStatusAsync(context.Package, PackageStatus.Available, false),
                Times.Once);
            context.StagedPackageRepository.Verify(
                x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                Times.Once);
            context.StagedPackageRepository.Verify(x => x.DeleteOnCommit(context.StagedPackage), Times.Once);
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Exactly(hasStagedSymbols ? 1 : 2));
            Assert.Null(context.StagedPackageIdentity.CurrentStagedPackageKey);
            Assert.Null(context.StagedPackageIdentity.CurrentStagedPackage);
            context.StagedPackageIdentityRepository.Verify(
                x => x.DeleteOnCommit(context.StagedPackageIdentity),
                hasStagedSymbols ? Times.Never() : Times.Once());
            Assert.Empty(context.StagedPackages);
            context.BlobCleanup.Verify(service => service.QueuePackageFiles(context.StagedPackageIdentity.Key), Times.Once);
            context.BlobCleanup.Verify(service => service.QueueSymbolFiles(context.StagedPackageIdentity.Key), hasStagedSymbols ? Times.Never() : Times.Once());
        }

        [Fact]
        public async Task ConsumesDuplicateDeliveryAfterPromotionCompletes()
        {
            var context = new TestContext();

            var firstHandled = await context.Target.HandleAsync(context.Message);
            context.StagedPackageIdentity.Owner.UserStatusKey = UserStatus.Locked;
            var duplicateHandled = await context.Target.HandleAsync(context.Message);

            Assert.True(firstHandled);
            Assert.True(duplicateHandled);
            context.PackageFileStorageService.Verify(
                x => x.CopyFileAsync(
                    context.SourceUri,
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg",
                    It.IsAny<IAccessCondition>()),
                Times.Once);
            context.StagedPackageRepository.Verify(
                x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                Times.Once);
            context.Notifications.Verify(service => service.SendAsync(context.StagedPackageIdentity.Owner,
                It.Is<StagingPromotionArtifact>(artifact => artifact.Succeeded && !artifact.Symbols)), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NotificationFailureDoesNotCompensateCommittedPublication(bool grouped)
        {
            var context = new TestContext();
            if (grouped)
            {
                context.AddToGroup();
            }

            context.Notifications.Setup(service => service.SendAsync(It.IsAny<User>(), It.IsAny<StagingPromotionArtifact>()))
                .ThrowsAsync(new InvalidOperationException("Email transport unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Target.HandleAsync(context.Message));

            Assert.Equal(PackageStatus.Available, context.Package.PackageStatusKey);
            if (grouped)
            {
                Assert.Same(context.StagedPackage, Assert.Single(context.StagedPackages));
                Assert.Equal(StagedPackageStatus.Succeeded, context.StagedPackage.Status);
            }
            else
            {
                Assert.Empty(context.StagedPackages);
            }

            context.PackageFileStorageService.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DispatchesAcceptedSymbolsOnlyAfterParentPublicationCommits(bool grouped)
        {
            var context = new TestContext();
            var symbols = context.AddAcceptedSymbols();
            if (grouped)
            {
                context.AddToGroup();
            }

            var transactionCompleted = false;
            context.StagedPackageRepository.Setup(repository => repository.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns(async (Func<Task> action) =>
                {
                    await action();
                    transactionCompleted = true;
                });
            context.MessageEnqueuer.Setup(enqueuer => enqueuer.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .Callback<StagingPromotionMessage>(message =>
                {
                    Assert.True(transactionCompleted);
                    Assert.Equal(PackageStatus.Available, context.Package.PackageStatusKey);
                    Assert.Equal(StagedPackageStatus.Succeeded, context.StagedPackage.Status);
                    Assert.Contains(context.StagedPackage, context.StagedPackages);
                    Assert.Equal(StagingPromotionTargetType.StagedSymbolPackage, message.TargetType);
                    Assert.Equal(symbols.Key, message.TargetKey);
                    Assert.Equal(context.PromotionId, message.PromotionId);
                }).Returns(Task.CompletedTask);

            Assert.True(await context.Target.HandleAsync(context.Message));

            if (grouped)
            {
                Assert.Same(context.StagedPackage, Assert.Single(context.StagedPackages));
                Assert.Equal(context.StagedPackage.Key, context.StagedPackageIdentity.CurrentStagedPackageKey);
            }
            else
            {
                Assert.Empty(context.StagedPackages);
                Assert.Null(context.StagedPackageIdentity.CurrentStagedPackageKey);
            }

            Assert.Equal(StagedPackageStatus.Promoting, symbols.Status);
            context.Notifications.Verify(service => service.SendAsync(context.StagedPackageIdentity.Owner,
                It.Is<StagingPromotionArtifact>(artifact => artifact.Succeeded && !artifact.Symbols)), Times.Once);
            context.Notifications.Verify(service => service.SendAsync(It.IsAny<User>(),
                It.Is<StagingPromotionArtifact>(artifact => artifact.Symbols)), Times.Never);
            Assert.NotNull(symbols.PromotionMessageSentDate);
            context.MessageEnqueuer.Verify(enqueuer => enqueuer.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FailedSymbolDispatchRetriesWithoutRepublishingOrCompensatingTheParent(bool grouped)
        {
            var context = new TestContext();
            var symbols = context.AddAcceptedSymbols();
            if (grouped)
            {
                context.AddToGroup();
            }

            context.MessageEnqueuer.Setup(enqueuer => enqueuer.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).ThrowsAsync(new TimeoutException());

            await Assert.ThrowsAsync<TimeoutException>(() => context.Target.HandleAsync(context.Message));

            Assert.Equal(PackageStatus.Available, context.Package.PackageStatusKey);
            Assert.Equal(StagedPackageStatus.Succeeded, context.StagedPackage.Status);
            Assert.Contains(context.StagedPackage, context.StagedPackages);
            Assert.Null(symbols.PromotionMessageSentDate);
            if (grouped)
            {
                context.Notifications.Verify(service => service.SendAsync(context.StagedPackageIdentity.Owner,
                    It.Is<StagingPromotionArtifact>(artifact => artifact.Succeeded && !artifact.Symbols)), Times.Once);
            }

            context.PackageFileStorageService.Verify(storage => storage.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            context.MessageEnqueuer.Setup(enqueuer => enqueuer.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).Returns(Task.CompletedTask);

            Assert.True(await context.Target.HandleAsync(context.Message));

            if (grouped)
            {
                Assert.Same(context.StagedPackage, Assert.Single(context.StagedPackages));
                context.StagingGroupPromotionService.Verify(service => service.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId), Times.Once);
                context.Notifications.Verify(service => service.SendAsync(context.StagedPackageIdentity.Owner,
                    It.Is<StagingPromotionArtifact>(artifact => artifact.Succeeded && !artifact.Symbols)), Times.Once);
            }
            else
            {
                Assert.Empty(context.StagedPackages);
            }

            context.PackageFileStorageService.Verify(storage => storage.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()), Times.Once);
            context.MessageEnqueuer.Verify(enqueuer => enqueuer.SendMessageAsync(It.Is<StagingPromotionMessage>(message =>
                message.TargetKey == symbols.Key && message.PromotionId == context.PromotionId)), Times.Exactly(2));
        }

        [Fact]
        public async Task CleanupFailureAfterSymbolDispatchDoesNotCompensateThePublishedParent()
        {
            var context = new TestContext();
            context.AddAcceptedSymbols();
            context.StagedPackageRepository.SetupSequence(repository => repository.CommitChangesAsync())
                .Returns(Task.CompletedTask)
                .ThrowsAsync(new DbUpdateConcurrencyException());

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.Target.HandleAsync(context.Message));

            Assert.Equal(PackageStatus.Available, context.Package.PackageStatusKey);
            context.MessageEnqueuer.Verify(enqueuer => enqueuer.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Once);
            context.PackageFileStorageService.Verify(storage => storage.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(StagedPackageStatus.Ready)]
        [InlineData(StagedPackageStatus.Promoting)]
        public async Task CompletedParentDoesNotDispatchSymbolsOutsideItsAcceptedPromotion(StagedPackageStatus symbolStatus)
        {
            var context = new TestContext();
            var symbols = context.AddAcceptedSymbols();
            symbols.Status = symbolStatus;
            symbols.ActivePromotionId = null;
            if (symbolStatus == StagedPackageStatus.Promoting)
            {
                symbols.ActivePromotionId = Guid.NewGuid();
            }

            context.Package.PackageStatusKey = PackageStatus.Available;
            context.StagedPackage.Status = StagedPackageStatus.Succeeded;

            Assert.True(await context.Target.HandleAsync(context.Message));

            Assert.Equal(symbolStatus, symbols.Status);
            Assert.Empty(context.StagedPackages);
            context.MessageEnqueuer.Verify(enqueuer => enqueuer.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            context.PackageFileStorageService.Verify(storage => storage.CopyFileAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IAccessCondition>()), Times.Never);
        }

        [Fact]
        public async Task ConsumesOrphanMessage()
        {
            var context = new TestContext();
            context.StagedPackages.Clear();

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
        }

        [Fact]
        public async Task ConsumesMessageForEarlierPromotionAttempt()
        {
            var context = new TestContext();
            context.StagedPackage.ActivePromotionId = Guid.NewGuid();

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
        }

        [Fact]
        public async Task ConsumesMessageWhenPromotionIsNoLongerActive()
        {
            var context = new TestContext();
            context.StagedPackage.Status = StagedPackageStatus.PromotionFailed;
            context.StagedPackage.ActivePromotionId = null;

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
        }

        [Fact]
        public async Task ConsumesMessageWhenPackageWasNeverPromoted()
        {
            var context = new TestContext();
            context.StagedPackage.Status = StagedPackageStatus.Ready;
            context.StagedPackage.ActivePromotionId = null;

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
        }

        [Fact]
        public async Task ConsumesMessageWhenPackageIsNoLongerStaged()
        {
            var context = new TestContext();
            context.Package.PackageStatusKey = PackageStatus.Available;

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
        }

        [Fact]
        public async Task FinalizesGroupAfterPackageSuccessCommits()
        {
            var context = new TestContext();
            context.AddToGroup();
            var packageSuccessCommitted = false;
            context.StagedPackageRepository
                .Setup(x => x.CommitChangesAsync())
                .Callback(() => packageSuccessCommitted = true)
                .Returns(Task.CompletedTask);
            context.StagingGroupPromotionService
                .Setup(x => x.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId))
                .Callback(() => Assert.True(packageSuccessCommitted))
                .Returns(Task.CompletedTask);

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Equal(StagedPackageStatus.Succeeded, context.StagedPackage.Status);
            context.StagingGroupPromotionService.Verify(
                x => x.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId),
                Times.Once);
        }

        [Fact]
        public async Task ResumesGroupFinalizationWithoutRepublishingSuccessfulPackage()
        {
            var context = new TestContext();
            context.AddToGroup();
            context.StagedPackage.Status = StagedPackageStatus.Succeeded;
            context.Package.PackageStatusKey = PackageStatus.Available;

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
            context.StagingGroupPromotionService.Verify(
                x => x.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId),
                Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GroupFinalizationFailureDoesNotLoseOrRepeatArtifactNotifications(bool succeeded)
        {
            var context = new TestContext();
            context.AddToGroup();
            var symbols = context.AddAcceptedSymbols();
            if (!succeeded)
            {
                context.Apply(InvalidState.MissingValidatedBlob);
            }

            context.Notifications.Setup(service => service.SendAsync(context.StagedPackageIdentity.Owner, It.IsAny<StagingPromotionArtifact>()))
                .Callback(() => context.StagedPackageRepository.Verify(repository => repository.CommitChangesAsync(), Times.AtLeastOnce()))
                .Returns(Task.CompletedTask);
            context.StagingGroupPromotionService.Setup(service => service.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId))
                .ThrowsAsync(new TimeoutException());

            await Assert.ThrowsAsync<TimeoutException>(() => context.Target.HandleAsync(context.Message));

            context.Notifications.Verify(service => service.SendAsync(context.StagedPackageIdentity.Owner,
                It.Is<StagingPromotionArtifact>(artifact => !artifact.Symbols && artifact.Succeeded == succeeded)), Times.Once);
            context.Notifications.Verify(service => service.SendAsync(context.StagedPackageIdentity.Owner,
                It.Is<StagingPromotionArtifact>(artifact => artifact.Symbols && !artifact.Succeeded)), succeeded ? Times.Never() : Times.Once());
            Assert.Equal(succeeded ? StagedPackageStatus.Succeeded : StagedPackageStatus.PromotionFailed, context.StagedPackage.Status);
            Assert.Equal(succeeded ? StagedPackageStatus.Promoting : StagedPackageStatus.PromotionFailed, symbols.Status);
            context.StagingGroupPromotionService.Setup(service => service.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId))
                .Returns(Task.CompletedTask);

            Assert.True(await context.Target.HandleAsync(context.Message));

            context.Notifications.Verify(service => service.SendAsync(It.IsAny<User>(), It.IsAny<StagingPromotionArtifact>()),
                Times.Exactly(succeeded ? 1 : 2));
            context.StagingGroupPromotionService.Verify(service => service.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId), Times.Exactly(2));
            context.PackageFileStorageService.Verify(service => service.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(InvalidState.MissingValidatedBlob)]
        [InlineData(InvalidState.OwnerNoLongerOwnsPackage)]
        [InlineData(InvalidState.OwnerNoLongerOwnsPackage, true)]
        public async Task MarksPromotionFailedWhenRequiredStateIsMissing(InvalidState state, bool grouped = false)
        {
            var context = new TestContext();
            var symbols = context.AddAcceptedSymbols();
            if (grouped)
            {
                context.AddToGroup();
            }

            context.Apply(state);

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Equal(StagedPackageStatus.PromotionFailed, context.StagedPackage.Status);
            Assert.Equal(StagedPackageStatus.PromotionFailed, symbols.Status);
            context.MessageEnqueuer.Verify(enqueuer => enqueuer.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            context.VerifyNotPublished();
            context.StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            context.Notifications.Verify(service => service.SendAsync(context.StagedPackageIdentity.Owner,
                It.Is<StagingPromotionArtifact>(artifact => !artifact.Symbols && !artifact.Succeeded)), Times.Once);
            context.Notifications.Verify(service => service.SendAsync(context.StagedPackageIdentity.Owner,
                It.Is<StagingPromotionArtifact>(artifact => artifact.Symbols && !artifact.Succeeded)), Times.Once);
        }

        [Fact]
        public async Task FinalizesGroupAfterPackageFailureCommits()
        {
            var context = new TestContext();
            context.AddToGroup();
            context.StagedPackage.ValidatedBlobPath = null;
            var packageFailureCommitted = false;
            context.StagedPackageRepository
                .Setup(x => x.CommitChangesAsync())
                .Callback(() => packageFailureCommitted = true)
                .Returns(Task.CompletedTask);
            context.StagingGroupPromotionService
                .Setup(x => x.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId))
                .Callback(() => Assert.True(packageFailureCommitted))
                .Returns(Task.CompletedTask);

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            Assert.Equal(StagedPackageStatus.PromotionFailed, context.StagedPackage.Status);
            context.StagingGroupPromotionService.Verify(
                x => x.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId),
                Times.Once);
        }

        [Fact]
        public async Task FinalizesGroupOnRedeliveryAfterPackageFailure()
        {
            var context = new TestContext();
            context.AddToGroup();
            context.StagedPackage.Status = StagedPackageStatus.PromotionFailed;

            var handled = await context.Target.HandleAsync(context.Message);

            Assert.True(handled);
            context.VerifyNotPublished();
            context.StagingGroupPromotionService.Verify(
                x => x.TryFinalizeAsync(context.StagingGroup.Key, context.PromotionId),
                Times.Once);
        }

        [Fact]
        public async Task ExtractsEmbeddedLicenseAndReadmeFromValidatedPackage()
        {
            var context = new TestContext();
            context.Package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
            context.Package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;
            context.Package.HasReadMe = true;

            await context.Target.HandleAsync(context.Message);

            context.LicenseFileService.Verify(
                x => x.ExtractAndSaveLicenseFileAsync(context.Package, It.IsAny<Stream>()),
                Times.Once);
            context.ReadmeFileService.Verify(
                x => x.ExtractAndSaveReadmeFileAsync(context.Package, It.IsAny<Stream>()),
                Times.Once);
            context.StagingBlobService.Verify(
                x => x.OpenPackageFileAsync(
                    context.StagedPackage.ValidatedBlobPath,
                    context.StagedPackage.ValidatedBlobETag),
                Times.Exactly(2));
        }

        [Fact]
        public async Task RemovesPublishedPackageWhenBlobPropertyUpdateFails()
        {
            var context = new TestContext();
            var expected = new InvalidOperationException("Property update failed.");
            context.PackageFileStorageService
                .Setup(x => x.SetPropertiesAsync(
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg",
                    It.IsAny<Func<Lazy<Task<Stream>>, ICloudBlobProperties, Task<bool>>>()))
                .ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Target.HandleAsync(context.Message));

            Assert.Same(expected, actual);
            Assert.Equal(StagedPackageStatus.Promoting, context.StagedPackage.Status);
            context.PackageFileStorageService.Verify(
                x => x.DeleteFileAsync(
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg"),
                Times.Once);
            context.StagedPackageRepository.Verify(
                x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                Times.Never);
        }

        [Fact]
        public async Task RemovesPublishedFilesWhenContentExtractionFails()
        {
            var context = new TestContext();
            var expected = new InvalidOperationException("Readme extraction failed.");
            context.Package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
            context.Package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;
            context.Package.HasReadMe = true;
            context.ReadmeFileService
                .Setup(x => x.ExtractAndSaveReadmeFileAsync(context.Package, It.IsAny<Stream>()))
                .ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Target.HandleAsync(context.Message));

            Assert.Same(expected, actual);
            Assert.Equal(StagedPackageStatus.Promoting, context.StagedPackage.Status);
            context.PackageFileStorageService.Verify(
                x => x.DeleteFileAsync(
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg"),
                Times.Once);
            context.LicenseFileService.Verify(
                x => x.DeleteLicenseFileAsync(context.Package.Id, context.Package.NormalizedVersion),
                Times.Once);
            context.ReadmeFileService.Verify(
                x => x.DeleteReadmeFileAsync(context.Package.Id, context.Package.NormalizedVersion),
                Times.Once);
            context.StagedPackageRepository.Verify(
                x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                Times.Never);
        }

        [Fact]
        public async Task RemovesPublishedPackageWhenDatabaseCommitFails()
        {
            var context = new TestContext();
            var expected = new InvalidOperationException("Database commit failed.");
            context.StagedPackageRepository
                .Setup(x => x.CommitChangesAsync())
                .ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Target.HandleAsync(context.Message));

            Assert.Same(expected, actual);
            Assert.Equal(StagedPackageStatus.Promoting, context.StagedPackage.Status);
            context.PackageFileStorageService.Verify(
                x => x.DeleteFileAsync(
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg"),
                Times.Once);
        }

        [Fact]
        public async Task KeepsPromotingWhenPackageCopyFails()
        {
            var context = new TestContext();
            var expected = new InvalidOperationException("Package copy failed.");
            context.PackageFileStorageService
                .Setup(x => x.CopyFileAsync(
                    context.SourceUri,
                    CoreConstants.Folders.PackagesFolderName,
                    "example.package.1.2.3.nupkg",
                    It.IsAny<IAccessCondition>()))
                .ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Target.HandleAsync(context.Message));

            Assert.Same(expected, actual);
            Assert.Equal(StagedPackageStatus.Promoting, context.StagedPackage.Status);
            context.PackageFileStorageService.Verify(
                x => x.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        public enum InvalidState
        {
            MissingValidatedBlob,
            OwnerNoLongerOwnsPackage,
        }

        private class TestContext
        {
            public TestContext()
            {
                Content = Encoding.UTF8.GetBytes("validated package content");
                SourceUri = new Uri("https://example.test/staging/validated.nupkg");
                PromotionId = Guid.NewGuid();
                var owner = new User { Key = 23, Username = "owner", EmailAddress = "owner@example.test" };
                Package = new Package
                {
                    Key = 11,
                    NormalizedVersion = "1.2.3",
                    PackageStatusKey = PackageStatus.Staged,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "Example.Package",
                        Owners = new List<User> { owner },
                        Packages = new List<Package>(),
                    },
                };
                Package.PackageRegistration.Packages.Add(Package);
                StagedPackageIdentity = new StagedPackageIdentity
                {
                    Key = Package.Key,
                    Package = Package,
                    OwnerKey = owner.Key,
                    Owner = owner,
                };
                StagedPackage = new StagedPackage
                {
                    Key = 42,
                    StagedPackageIdentityKey = StagedPackageIdentity.Key,
                    StagedPackageIdentity = StagedPackageIdentity,
                    Status = StagedPackageStatus.Promoting,
                    ActivePromotionId = PromotionId,
                    ValidatedBlobPath = "example.package/1.2.3/validated.nupkg",
                    ValidatedBlobETag = "\"validated\"",
                    UploadedBlobPath = "example.package/1.2.3/uploaded.nupkg",
                    UploadedBlobETag = "\"uploaded\"",
                    UploadHash = "upload-hash",
                };
                StagedPackageIdentity.CurrentStagedPackageKey = StagedPackage.Key;
                StagedPackageIdentity.CurrentStagedPackage = StagedPackage;
                Message = StagingPromotionMessage.ForPackage(PromotionId, StagedPackage.Key);
                StagedPackages = new List<StagedPackage> { StagedPackage };

                StagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                StagedPackageIdentityRepository = new Mock<IEntityRepository<StagedPackageIdentity>>();
                StagedPackageIdentityRepository
                    .Setup(x => x.DeleteOnCommit(It.IsAny<StagedPackageIdentity>()))
                    .Callback(() => StagedPackageRepository.Verify(x => x.CommitChangesAsync(), Times.Once));
                StagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(() => StagedPackages.AsQueryable());
                StagedPackageRepository
                    .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                    .Returns((Func<Task> action) => action());
                StagedPackageRepository
                    .Setup(x => x.DeleteOnCommit(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(stagedPackage => StagedPackages.Remove(stagedPackage));
                PackageService = new Mock<ICorePackageService>();
                PackageService
                    .Setup(x => x.UpdatePackageStreamMetadataAsync(Package, It.IsAny<NuGetGallery.Packaging.PackageStreamMetadata>(), false))
                    .Returns<Package, NuGetGallery.Packaging.PackageStreamMetadata, bool>((package, metadata, _) =>
                    {
                        package.Hash = metadata.Hash;
                        package.HashAlgorithm = metadata.HashAlgorithm;
                        package.PackageFileSize = metadata.Size;
                        return Task.CompletedTask;
                    });
                PackageService
                    .Setup(x => x.UpdatePackageStatusAsync(Package, PackageStatus.Available, false))
                    .Returns<Package, PackageStatus, bool>((package, status, _) =>
                    {
                        package.PackageStatusKey = status;
                        return Task.CompletedTask;
                    });

                StagingBlobService = new Mock<IStagingBlobService>();
                StagingBlobService
                    .Setup(x => x.OpenPackageFileAsync(StagedPackage.ValidatedBlobPath, StagedPackage.ValidatedBlobETag))
                    .ReturnsAsync(() => new MemoryStream(Content));
                StagingBlobService
                    .Setup(x => x.GetPackageReadUriAsync(StagedPackage.ValidatedBlobPath, StagedPackage.ValidatedBlobETag))
                    .ReturnsAsync(SourceUri);

                PublicBlobProperties = new Mock<ICloudBlobProperties>();
                PublicBlobProperties.SetupProperty(x => x.CacheControl, "private");
                PackageFileStorageService = new Mock<ICoreFileStorageService>();
                PackageFileStorageService
                    .Setup(x => x.SetPropertiesAsync(
                        CoreConstants.Folders.PackagesFolderName,
                        "example.package.1.2.3.nupkg",
                        It.IsAny<Func<Lazy<Task<Stream>>, ICloudBlobProperties, Task<bool>>>()))
                    .Returns<string, string, Func<Lazy<Task<Stream>>, ICloudBlobProperties, Task<bool>>>(
                        (_, __, update) => update(null, PublicBlobProperties.Object));

                LicenseFileService = new Mock<ICoreLicenseFileService>();
                ReadmeFileService = new Mock<ICoreReadmeFileService>();
                StagingGroupPromotionService = new Mock<IStagingGroupPromotionService>();
                StagingGroupPromotionService
                    .Setup(x => x.MarkPackageSucceeded(It.IsAny<StagedPackage>()))
                    .Callback<StagedPackage>(stagedPackage => stagedPackage.Status = StagedPackageStatus.Succeeded);
                StagingGroupPromotionService
                    .Setup(x => x.TryFinalizeAsync(It.IsAny<int>(), It.IsAny<Guid>()))
                    .Returns(Task.CompletedTask);
                MessageEnqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
                Target = new StagedPackagePromotionMessageHandler(
                    StagedPackageRepository.Object,
                    StagedPackageIdentityRepository.Object,
                    StagingGroupPromotionService.Object,
                    MessageEnqueuer.Object,
                    PackageService.Object,
                    StagingBlobService.Object,
                    PackageFileStorageService.Object,
                    new PackageFileMetadataService(),
                    LicenseFileService.Object,
                    ReadmeFileService.Object,
                    BlobCleanup.Object,
                    Notifications.Object,
                    Mock.Of<ILogger<StagedPackagePromotionMessageHandler>>());
            }

            public StagedSymbolPackage AddAcceptedSymbols()
            {
                var symbols = new StagedSymbolPackage
                {
                    Key = 100,
                    StagedPackageIdentity = StagedPackageIdentity,
                    StagedPackageIdentityKey = StagedPackageIdentity.Key,
                    SymbolPackage = new SymbolPackage { Package = Package, PackageKey = Package.Key, StatusKey = PackageStatus.Staged },
                    Status = StagedPackageStatus.Promoting,
                    ActivePromotionId = PromotionId,
                };
                StagedPackageIdentity.CurrentStagedSymbolPackageKey = symbols.Key;
                StagedPackageIdentity.CurrentStagedSymbolPackage = symbols;
                return symbols;
            }

            public Mock<IStagingBlobCleanupService> BlobCleanup { get; } = new Mock<IStagingBlobCleanupService>();

            public Mock<IStagingPromotionNotificationService> Notifications { get; } = new Mock<IStagingPromotionNotificationService>();

            public void AddToGroup()
            {
                StagingGroup = new StagingGroup
                {
                    Key = 7,
                    ActivePromotionId = PromotionId,
                };
                StagedPackageIdentity.StagingGroupKey = StagingGroup.Key;
                StagedPackageIdentity.StagingGroup = StagingGroup;
            }

            public void Apply(InvalidState state)
            {
                switch (state)
                {
                    case InvalidState.MissingValidatedBlob:
                        StagedPackage.ValidatedBlobPath = null;
                        break;
                    case InvalidState.OwnerNoLongerOwnsPackage:
                        Package.PackageRegistration.Owners.Clear();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state));
                }
            }

            public void VerifyNotPublished()
            {
                PackageFileStorageService.Verify(
                    x => x.CopyFileAsync(
                        It.IsAny<Uri>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
                StagedPackageRepository.Verify(
                    x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()),
                    Times.Never);
            }

            public byte[] Content { get; }
            public Uri SourceUri { get; }
            public Guid PromotionId { get; }
            public Package Package { get; }
            public StagedPackageIdentity StagedPackageIdentity { get; }
            public StagedPackage StagedPackage { get; }
            public StagingGroup StagingGroup { get; private set; }
            public StagingPromotionMessage Message { get; }
            public List<StagedPackage> StagedPackages { get; }
            public Mock<IEntityRepository<StagedPackage>> StagedPackageRepository { get; }
            public Mock<IEntityRepository<StagedPackageIdentity>> StagedPackageIdentityRepository { get; }
            public Mock<IStagingGroupPromotionService> StagingGroupPromotionService { get; }
            public Mock<IStagingPromotionMessageEnqueuer> MessageEnqueuer { get; }
            public Mock<ICorePackageService> PackageService { get; }
            public Mock<IStagingBlobService> StagingBlobService { get; }
            public Mock<ICoreFileStorageService> PackageFileStorageService { get; }
            public Mock<ICloudBlobProperties> PublicBlobProperties { get; }
            public Mock<ICoreLicenseFileService> LicenseFileService { get; }
            public Mock<ICoreReadmeFileService> ReadmeFileService { get; }
            public StagedPackagePromotionMessageHandler Target { get; }
        }
    }
}

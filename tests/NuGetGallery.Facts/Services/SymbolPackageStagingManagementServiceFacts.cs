// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class SymbolPackageStagingManagementServiceFacts
    {
        [Fact]
        public void ListsOnlyCurrentAuthorizedStagedSymbols()
        {
            var context = new TestContext();
            var superseded = context.AddAttempt(2);
            superseded.StagedPackageIdentity = context.Attempt.StagedPackageIdentity;
            superseded.StagedPackageIdentityKey = context.Attempt.StagedPackageIdentityKey;
            superseded.Status = StagedPackageStatus.Superseded;
            var publicSymbols = context.AddAttempt(4);
            publicSymbols.SymbolPackage.StatusKey = PackageStatus.Available;
            var otherOwner = context.AddAttempt(5);
            otherOwner.StagedPackageIdentity.OwnerKey = 999;
            var lostPermission = context.AddAttempt(6);
            context.AuthorizationService.Setup(x => x.CanManage(context.Owner, lostPermission)).Returns(false);

            Assert.Same(context.Attempt, Assert.Single(context.Target.GetStagedSymbolPackages(context.Owner)));
            context.Attempt.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Deleted;
            Assert.Same(context.Attempt, Assert.Single(context.Target.GetStagedSymbolPackages(context.Owner)));
        }

        [Fact]
        public async Task DownloadsExactUploadedSymbolContent()
        {
            var context = new TestContext();
            using var content = new MemoryStream(new byte[] { 1, 2, 3 });
            context.BlobService.Setup(x => x.OpenPackageFileAsync("symbols.snupkg", "etag")).ReturnsAsync(content);

            Assert.Same(content, await context.Target.OpenPackageContentAsync(context.Attempt));
            context.BlobService.Verify(x => x.OpenPackageFileAsync("symbols.snupkg", "etag"), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [InlineData(true, true)]
        public async Task DeletesOnlyPrivateSymbolsAndRemovesEmptyIdentity(bool hasStagedPackage, bool retainedContent = false)
        {
            var context = new TestContext();
            var identity = context.Attempt.StagedPackageIdentity;
            var previousAttempt = context.AddAttempt(2);
            previousAttempt.StagedPackageIdentityKey = identity.Key;
            previousAttempt.StagedPackageIdentity = identity;
            previousAttempt.Status = StagedPackageStatus.Superseded;
            if (retainedContent)
            {
                previousAttempt.SymbolPackage = context.Attempt.SymbolPackage;
            }
            if (hasStagedPackage)
            {
                identity.CurrentStagedPackageKey = 42;
            }

            var publicSymbols = new SymbolPackage { Key = 100, StatusKey = PackageStatus.Available };
            identity.Package.SymbolPackages.Add(publicSymbols);
            context.IdentityRepository.Setup(x => x.DeleteOnCommit(identity))
                .Callback(() => context.AttemptRepository.Verify(x => x.CommitChangesAsync(), Times.Once));
            context.SymbolRepository.Setup(x => x.DeleteOnCommit(context.Attempt.SymbolPackage))
                .Callback(() => context.AttemptRepository.Verify(x => x.CommitChangesAsync(), Times.Once));

            Assert.True(await context.Target.DeletePackageAsync(context.Attempt));

            Assert.Null(identity.CurrentStagedSymbolPackageKey);
            Assert.Null(identity.CurrentStagedSymbolPackage);
            Assert.Equal(PackageStatus.Available, identity.Package.PackageStatusKey);
            Assert.Equal(PackageStatus.Available, publicSymbols.StatusKey);
            context.AttemptRepository.Verify(x => x.DeleteOnCommit(context.Attempt), Times.Once);
            context.AttemptRepository.Verify(x => x.DeleteOnCommit(previousAttempt), Times.Once);
            context.SymbolRepository.Verify(x => x.DeleteOnCommit(previousAttempt.SymbolPackage), Times.Once);
            context.SymbolRepository.Verify(x => x.DeleteOnCommit(context.Attempt.SymbolPackage), Times.Once);
            context.SymbolRepository.Verify(x => x.DeleteOnCommit(publicSymbols), Times.Never);
            context.IdentityRepository.Verify(x => x.DeleteOnCommit(identity), hasStagedPackage ? Times.Never() : Times.Once());
            context.AttemptRepository.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()), Times.Once);
            context.AttemptRepository.Verify(x => x.CommitChangesAsync(), Times.Exactly(2));
            context.BlobService.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DoesNotDeleteDuringPromotion(bool groupPromotion)
        {
            var context = new TestContext();
            if (groupPromotion)
            {
                context.Attempt.StagedPackageIdentity.StagingGroup = new StagingGroup { ActivePromotionId = Guid.NewGuid() };
            }
            else
            {
                context.Attempt.Status = StagedPackageStatus.Promoting;
            }

            Assert.False(await context.Target.DeletePackageAsync(context.Attempt));

            Assert.Equal(context.Attempt.Key, context.Attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
            context.AttemptRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedSymbolPackage>()), Times.Never);
            context.AttemptRepository.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()), Times.Never);
            context.AttemptRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task DoesNotDeleteSymbolsThatAreNoLongerPrivate()
        {
            var context = new TestContext();
            context.Attempt.SymbolPackage.StatusKey = PackageStatus.Available;

            Assert.False(await context.Target.DeletePackageAsync(context.Attempt));

            context.SymbolRepository.Verify(x => x.DeleteOnCommit(It.IsAny<SymbolPackage>()), Times.Never);
            context.AttemptRepository.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()), Times.Never);
            context.AttemptRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ReportsConcurrentDeletionAsConflict()
        {
            var context = new TestContext();
            context.AttemptRepository.Setup(x => x.CommitChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException());

            Assert.False(await context.Target.DeletePackageAsync(context.Attempt));

            context.IdentityRepository.Verify(x => x.DeleteOnCommit(It.IsAny<StagedPackageIdentity>()), Times.Never);
            context.SymbolRepository.Verify(x => x.DeleteOnCommit(It.IsAny<SymbolPackage>()), Times.Never);
        }

        private class TestContext
        {
            public TestContext()
            {
                AuthorizationService.Setup(x => x.GetEnabledOwners(Owner)).Returns(new[] { Owner });
                AuthorizationService.Setup(x => x.CanManage(Owner, It.IsAny<StagedSymbolPackage>())).Returns(true);
                AttemptRepository.Setup(x => x.GetAll()).Returns(() => Attempts.AsQueryable());
                AttemptRepository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>())).Returns<Func<Task>>(action => action());
                AttemptRepository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
                Attempt = AddAttempt(1);
                Target = new SymbolPackageStagingManagementService(
                    AuthorizationService.Object,
                    Mock.Of<IPackageService>(),
                    AttemptRepository.Object,
                    IdentityRepository.Object,
                    SymbolRepository.Object,
                    BlobService.Object);
            }

            public StagedSymbolPackage AddAttempt(int key)
            {
                var identity = new StagedPackageIdentity
                {
                    Key = key,
                    OwnerKey = Owner.Key,
                    Owner = Owner,
                    Package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Test.Package" },
                        PackageStatusKey = PackageStatus.Available,
                    },
                    CurrentStagedSymbolPackageKey = key,
                };
                var attempt = new StagedSymbolPackage
                {
                    Key = key,
                    StagedPackageIdentityKey = key,
                    StagedPackageIdentity = identity,
                    SymbolPackage = new SymbolPackage { Key = key, Package = identity.Package, StatusKey = PackageStatus.Staged },
                    Status = StagedPackageStatus.Ready,
                    UploadedBlobPath = "symbols.snupkg",
                    UploadedBlobETag = "etag",
                };
                identity.CurrentStagedSymbolPackage = attempt;
                Attempts.Add(attempt);
                return attempt;
            }

            public User Owner { get; } = new User("owner") { Key = 10 };

            public List<StagedSymbolPackage> Attempts { get; } = new List<StagedSymbolPackage>();

            public StagedSymbolPackage Attempt { get; }

            public Mock<IPackageStagingAuthorizationService> AuthorizationService { get; } = new Mock<IPackageStagingAuthorizationService>();

            public Mock<IEntityRepository<StagedSymbolPackage>> AttemptRepository { get; } = new Mock<IEntityRepository<StagedSymbolPackage>>();

            public Mock<IEntityRepository<StagedPackageIdentity>> IdentityRepository { get; } = new Mock<IEntityRepository<StagedPackageIdentity>>();

            public Mock<IEntityRepository<SymbolPackage>> SymbolRepository { get; } = new Mock<IEntityRepository<SymbolPackage>>();

            public Mock<IStagingBlobService> BlobService { get; } = new Mock<IStagingBlobService>();

            public SymbolPackageStagingManagementService Target { get; }
        }
    }
}

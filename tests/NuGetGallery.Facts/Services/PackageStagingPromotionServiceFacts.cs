// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using Xunit;

namespace NuGetGallery
{
    public class PackageStagingPromotionServiceFacts
    {
        private const int StagedPackageKey = 456;

        [Fact]
        public async Task AcceptsAuthorizedReadyPackageBeforeSendingMessage()
        {
            var events = new List<string>();
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Ready);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            SetupTransaction(repository, stagedPackage, events);
            repository
                .Setup(x => x.CommitChangesAsync())
                .Callback(() => events.Add($"Commit:{stagedPackage.Status}"))
                .Returns(Task.CompletedTask);
            StagingPromotionMessage message = null;
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer
                .Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .Callback<StagingPromotionMessage>(value =>
                {
                    events.Add("Send");
                    message = value;
                })
                .Returns(Task.CompletedTask);
            var target = CreateService(repository, enqueuer);

            var result = await target.PromotePackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Accepted, result);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.Equal(stagedPackage.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagedPackage, message.TargetType);
            Assert.Equal(StagedPackageKey, message.TargetKey);
            Assert.Equal(new[] { "Transaction", "Commit:Promoting", "Send" }, events);
        }

        [Fact]
        public async Task RejectsUnauthorizedPackage()
        {
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Ready);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer, authorized: false);

            var result = await target.PromotePackageAsync(new User("other"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Unauthorized, result);
            Assert.Equal(StagedPackageStatus.Ready, stagedPackage.Status);
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task RejectsPackageThatIsAlreadyPromoting()
        {
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Promoting);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            var result = await target.PromotePackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.NotReady, result);
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task RejectsPackageThatBelongsToAGroup()
        {
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Ready);
            stagedPackage.StagedPackageIdentity.StagingGroupKey = 10;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            var result = await target.PromotePackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Grouped, result);
            Assert.Equal(StagedPackageStatus.Ready, stagedPackage.Status);
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task ReturnsConflictWhenMembershipChangeWinsTheRace()
        {
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Ready);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            SetupTransaction(repository, stagedPackage);
            repository
                .Setup(x => x.CommitChangesAsync())
                .ThrowsAsync(new DbUpdateConcurrencyException());
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            var result = await target.PromotePackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Conflict, result);
            Assert.Equal(StagedPackageStatus.Ready, stagedPackage.Status);
            Assert.Null(stagedPackage.ActivePromotionId);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task RollsBackAcceptanceWhenSendingFails()
        {
            var committedStatuses = new List<StagedPackageStatus>();
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Ready);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            SetupTransaction(repository, stagedPackage);
            repository
                .Setup(x => x.CommitChangesAsync())
                .Callback(() => committedStatuses.Add(stagedPackage.Status))
                .Returns(Task.CompletedTask);
            var expectedException = new InvalidOperationException("Send failed.");
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer
                .Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .ThrowsAsync(expectedException);
            var target = CreateService(repository, enqueuer);

            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.PromotePackageAsync(new User("owner"), stagedPackage));

            Assert.Same(expectedException, actualException);
            Assert.Equal(StagedPackageStatus.Ready, stagedPackage.Status);
            Assert.Null(stagedPackage.ActivePromotionId);
            Assert.Equal(
                new[] { StagedPackageStatus.Promoting },
                committedStatuses);
        }

        private static PackageStagingPromotionService CreateService(
            Mock<IEntityRepository<StagedPackage>> repository,
            Mock<IStagingPromotionMessageEnqueuer> enqueuer,
            bool authorized = true)
        {
            var authorizationService = new Mock<IPackageStagingAuthorizationService>();
            authorizationService
                .Setup(x => x.CanManage(It.IsAny<User>(), It.IsAny<StagedPackage>()))
                .Returns(authorized);

            return new PackageStagingPromotionService(
                authorizationService.Object,
                enqueuer.Object,
                repository.Object);
        }

        private static StagedPackage CreateStagedPackage(StagedPackageStatus status)
        {
            var owner = new User("owner") { Key = 1 };
            var package = new Package { Key = 42 };
            var identity = new StagedPackageIdentity
            {
                Key = package.Key,
                Package = package,
                OwnerKey = owner.Key,
                Owner = owner,
            };
            var stagedPackage = new StagedPackage
            {
                Key = StagedPackageKey,
                StagedPackageIdentityKey = identity.Key,
                StagedPackageIdentity = identity,
                Status = status,
            };
            identity.CurrentStagedPackageKey = stagedPackage.Key;
            identity.CurrentStagedPackage = stagedPackage;
            return stagedPackage;
        }

        private static void SetupTransaction(
            Mock<IEntityRepository<StagedPackage>> repository,
            StagedPackage stagedPackage,
            List<string> events = null)
        {
            repository
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(async action =>
                {
                    events?.Add("Transaction");
                    var originalPromotionId = stagedPackage.ActivePromotionId;
                    var originalStatus = stagedPackage.Status;

                    try
                    {
                        await action();
                    }
                    catch
                    {
                        stagedPackage.ActivePromotionId = originalPromotionId;
                        stagedPackage.Status = originalStatus;
                        throw;
                    }
                });
        }
    }
}

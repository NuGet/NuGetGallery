// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
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
        public async Task CommitsAuthorizedReadyPackageBeforeSendingMessage()
        {
            var events = new List<string>();
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Ready);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            var saveCompleted = new TaskCompletionSource<bool>();
            repository
                .Setup(x => x.CommitChangesAsync())
                .Callback(() => events.Add($"Commit:{stagedPackage.Status}"))
                .Returns(saveCompleted.Task);
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

            var promotion = target.PromotePackageAsync(new User("owner"), stagedPackage);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            saveCompleted.SetResult(true);
            var result = await promotion;

            Assert.Equal(PackageStagingPromotionResult.Accepted, result);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.Equal(stagedPackage.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagedPackage, message.TargetType);
            Assert.Equal(StagedPackageKey, message.TargetKey);
            Assert.Equal(new[] { "Commit:Promoting", "Send" }, events);
        }

        [Fact]
        public async Task ResendsStalledPackageWithSamePromotionId()
        {
            var events = new List<string>();
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Promoting);
            stagedPackage.ActivePromotionId = Guid.NewGuid();
            var previousSentDate = DateTime.UtcNow.AddMinutes(-61);
            stagedPackage.PromotionMessageSentDate = previousSentDate;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.CommitChangesAsync())
                .Callback(() => events.Add("Commit"))
                .Returns(Task.CompletedTask);
            StagingPromotionMessage message = null;
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .Callback<StagingPromotionMessage>(value =>
                {
                    events.Add("Send");
                    message = value;
                })
                .Returns(Task.CompletedTask);
            var target = CreateService(repository, enqueuer);

            var result = await target.ResendPackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Accepted, result);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.Equal(stagedPackage.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagedPackage, message.TargetType);
            Assert.Equal(stagedPackage.Key, message.TargetKey);
            Assert.True(stagedPackage.PromotionMessageSentDate > previousSentDate);
            Assert.Equal(new[] { "Commit", "Send" }, events);
            repository.Verify(x => x.CommitChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task MakesPackageImmediatelyRetryableWhenResendFails()
        {
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Promoting);
            stagedPackage.ActivePromotionId = Guid.NewGuid();
            stagedPackage.PromotionMessageSentDate = DateTime.UtcNow.AddMinutes(-61);
            var promotionId = stagedPackage.ActivePromotionId;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .ThrowsAsync(new ServiceBusException("Send failed.", ServiceBusFailureReason.ServiceTimeout));
            var target = CreateService(repository, enqueuer);

            var result = await target.ResendPackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.DispatchFailed, result);
            Assert.Equal(promotionId, stagedPackage.ActivePromotionId);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.True(StagingPromotionResendPolicy.IsDue(stagedPackage.PromotionMessageSentDate));
            repository.Verify(x => x.CommitChangesAsync(), Times.Exactly(2));
        }

        [Theory]
        [InlineData(StagedPackageStatus.Promoting, -59)]
        [InlineData(StagedPackageStatus.PromotionFailed, -120)]
        public async Task DoesNotResendPackageBeforeDelayOrAfterFailure(StagedPackageStatus status, int minutesAgo)
        {
            var stagedPackage = CreateStagedPackage(status);
            stagedPackage.ActivePromotionId = Guid.NewGuid();
            stagedPackage.PromotionMessageSentDate = DateTime.UtcNow.AddMinutes(minutesAgo);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            var result = await target.ResendPackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.NotReady, result);
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
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
            repository
                .Setup(x => x.CommitChangesAsync())
                .ThrowsAsync(new DbUpdateConcurrencyException());
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            var result = await target.PromotePackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Conflict, result);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task MakesCommittedPackageImmediatelyRetryableWhenSendingFails()
        {
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Ready);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer
                .Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .ThrowsAsync(new TimeoutException("Send timed out."));
            var target = CreateService(repository, enqueuer);

            var result = await target.PromotePackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.DispatchFailed, result);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.NotNull(stagedPackage.ActivePromotionId);
            Assert.True(StagingPromotionResendPolicy.IsDue(stagedPackage.PromotionMessageSentDate));
            repository.Verify(x => x.CommitChangesAsync(), Times.Exactly(2));

            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).Returns(Task.CompletedTask);
            Assert.Equal(PackageStagingPromotionResult.Accepted, await target.ResendPackageAsync(new User("owner"), stagedPackage));
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.False(StagingPromotionResendPolicy.IsDue(stagedPackage.PromotionMessageSentDate));
        }

        [Fact]
        public async Task ReportsConflictWhenSendFailureRecoveryLosesARace()
        {
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Ready);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            var commits = 0;
            repository.Setup(x => x.CommitChangesAsync()).Returns(() =>
            {
                if (++commits == 2)
                {
                    throw new DbUpdateConcurrencyException();
                }

                return Task.CompletedTask;
            });
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .ThrowsAsync(new TimeoutException("Send timed out."));
            var target = CreateService(repository, enqueuer);

            var result = await target.PromotePackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Conflict, result);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.NotNull(stagedPackage.ActivePromotionId);
        }

        [Fact]
        public async Task CommitsAuthorizedReadyGroupBeforeSendingMessage()
        {
            var events = new List<string>();
            var group = CreateStagingGroup();
            var stagedPackages = new[]
            {
                CreateStagedPackage(StagedPackageStatus.Ready, key: 456, group),
                CreateStagedPackage(StagedPackageStatus.Ready, key: 789, group),
            };
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(stagedPackages.AsQueryable());
            repository
                .Setup(x => x.CommitChangesAsync())
                .Callback(() => events.Add("Commit:Promoting"))
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

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.Accepted, result);
            Assert.Equal(group.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagingGroup, message.TargetType);
            Assert.Equal(group.Key, message.TargetKey);
            Assert.Equal(0, group.MutationRevision);
            Assert.All(stagedPackages, stagedPackage =>
            {
                Assert.Equal(group.ActivePromotionId, stagedPackage.ActivePromotionId);
                Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            });
            Assert.Equal(new[] { "Commit:Promoting", "Send" }, events);
        }

        [Fact]
        public async Task ResendsStalledGroupWithSamePromotionId()
        {
            var events = new List<string>();
            var group = CreateStagingGroup();
            group.ActivePromotionId = Guid.NewGuid();
            group.MutationRevision = 3;
            var previousSentDate = DateTime.UtcNow.AddMinutes(-61);
            group.PromotionMessageSentDate = previousSentDate;
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Promoting, group: group);
            stagedPackage.ActivePromotionId = group.ActivePromotionId;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.CommitChangesAsync())
                .Callback(() => events.Add("Commit"))
                .Returns(Task.CompletedTask);
            StagingPromotionMessage message = null;
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .Callback<StagingPromotionMessage>(value =>
                {
                    events.Add("Send");
                    message = value;
                })
                .Returns(Task.CompletedTask);
            var target = CreateService(repository, enqueuer);

            var result = await target.ResendGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.Accepted, result);
            Assert.Equal(group.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagingGroup, message.TargetType);
            Assert.Equal(group.Key, message.TargetKey);
            Assert.Equal(3, group.MutationRevision);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.Equal(group.ActivePromotionId, stagedPackage.ActivePromotionId);
            Assert.True(group.PromotionMessageSentDate > previousSentDate);
            Assert.Equal(new[] { "Commit", "Send" }, events);
            repository.Verify(x => x.CommitChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task MakesGroupImmediatelyRetryableWhenResendFails()
        {
            var group = CreateStagingGroup();
            group.ActivePromotionId = Guid.NewGuid();
            group.PromotionMessageSentDate = DateTime.UtcNow.AddMinutes(-61);
            var promotionId = group.ActivePromotionId;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .ThrowsAsync(new TimeoutException("Send timed out."));
            var target = CreateService(repository, enqueuer);

            var result = await target.ResendGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.DispatchFailed, result);
            Assert.Equal(promotionId, group.ActivePromotionId);
            Assert.True(StagingPromotionResendPolicy.IsDue(group.PromotionMessageSentDate));
            repository.Verify(x => x.CommitChangesAsync(), Times.Exactly(2));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DoesNotResendGroupWithoutActiveTimedOutPromotion(bool isActive)
        {
            var group = CreateStagingGroup();
            group.ActivePromotionId = isActive ? Guid.NewGuid() : null;
            group.PromotionMessageSentDate = isActive ? DateTime.UtcNow.AddMinutes(-59) : DateTime.UtcNow.AddHours(-2);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            var result = await target.ResendGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.NotReady, result);
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task RejectsGroupWhenAnyPackageIsUnauthorized()
        {
            var group = CreateStagingGroup();
            var stagedPackages = new[]
            {
                CreateStagedPackage(StagedPackageStatus.Ready, key: 456, group),
                CreateStagedPackage(StagedPackageStatus.Ready, key: 789, group),
            };
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(stagedPackages.AsQueryable());
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer, authorizedPackageKey: 456);

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.Unauthorized, result);
            Assert.All(stagedPackages, stagedPackage =>
            {
                Assert.Null(stagedPackage.ActivePromotionId);
                Assert.Equal(StagedPackageStatus.Ready, stagedPackage.Status);
            });
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task RejectsGroupWhenAnyPackageIsNotReady()
        {
            var group = CreateStagingGroup();
            var stagedPackages = new[]
            {
                CreateStagedPackage(StagedPackageStatus.Ready, key: 456, group),
                CreateStagedPackage(StagedPackageStatus.Validating, key: 789, group),
            };
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(stagedPackages.AsQueryable());
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.NotReady, result);
            Assert.All(stagedPackages, stagedPackage => Assert.Null(stagedPackage.ActivePromotionId));
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task ReturnsConflictWhenGroupAcceptanceLosesARace()
        {
            var group = CreateStagingGroup();
            var stagedPackages = new[] { CreateStagedPackage(StagedPackageStatus.Ready, key: 456, group) };
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(stagedPackages.AsQueryable());
            repository
                .Setup(x => x.CommitChangesAsync())
                .ThrowsAsync(new DbUpdateConcurrencyException());
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.Conflict, result);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task MakesCommittedGroupImmediatelyRetryableWhenSendingFails()
        {
            var group = CreateStagingGroup();
            var stagedPackages = new[] { CreateStagedPackage(StagedPackageStatus.Ready, key: 456, group) };
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(stagedPackages.AsQueryable());
            repository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer
                .Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .ThrowsAsync(new TimeoutException("Send timed out."));
            var target = CreateService(repository, enqueuer);

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.DispatchFailed, result);
            Assert.NotNull(group.ActivePromotionId);
            Assert.Equal(group.ActivePromotionId, stagedPackages[0].ActivePromotionId);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackages[0].Status);
            Assert.True(StagingPromotionResendPolicy.IsDue(group.PromotionMessageSentDate));
            repository.Verify(x => x.CommitChangesAsync(), Times.Exactly(2));

            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).Returns(Task.CompletedTask);
            Assert.Equal(StagingGroupPromotionResult.Accepted, await target.ResendGroupAsync(new User("owner") { Key = 1 }, group));
            Assert.Equal(group.ActivePromotionId, stagedPackages[0].ActivePromotionId);
            Assert.False(StagingPromotionResendPolicy.IsDue(group.PromotionMessageSentDate));
        }

        private static PackageStagingPromotionService CreateService(
            Mock<IEntityRepository<StagedPackage>> repository,
            Mock<IStagingPromotionMessageEnqueuer> enqueuer,
            bool authorized = true,
            int? authorizedPackageKey = null)
        {
            var authorizationService = new Mock<IPackageStagingAuthorizationService>();
            authorizationService
                .Setup(x => x.CanManage(It.IsAny<User>(), It.IsAny<StagedPackage>()))
                .Returns<User, StagedPackage>((currentUser, stagedPackage) =>
                    authorized && (!authorizedPackageKey.HasValue || stagedPackage.Key == authorizedPackageKey.Value));
            authorizationService
                .Setup(x => x.GetEnabledOwners(It.IsAny<User>()))
                .Returns<User>(currentUser => new[] { currentUser });

            return new PackageStagingPromotionService(
                authorizationService.Object,
                enqueuer.Object,
                repository.Object);
        }

        private static StagedPackage CreateStagedPackage(StagedPackageStatus status, int key = StagedPackageKey, StagingGroup group = null)
        {
            var owner = new User("owner") { Key = 1 };
            var package = new Package { Key = key + 1, PackageStatusKey = PackageStatus.Staged };
            var identity = new StagedPackageIdentity
            {
                Key = package.Key,
                Package = package,
                OwnerKey = owner.Key,
                Owner = owner,
                StagingGroupKey = group?.Key,
                StagingGroup = group,
            };
            var stagedPackage = new StagedPackage
            {
                Key = key,
                StagedPackageIdentityKey = identity.Key,
                StagedPackageIdentity = identity,
                Status = status,
            };
            identity.CurrentStagedPackageKey = stagedPackage.Key;
            identity.CurrentStagedPackage = stagedPackage;
            return stagedPackage;
        }

        private static StagingGroup CreateStagingGroup()
        {
            var owner = new User("owner") { Key = 1 };
            return new StagingGroup
            {
                Key = 12,
                OwnerKey = owner.Key,
                Owner = owner,
                Id = "group",
                Name = "Group",
            };
        }

    }
}

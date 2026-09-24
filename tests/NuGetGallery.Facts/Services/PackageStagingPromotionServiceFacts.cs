// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
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
        public async Task ResendsStalledPackageWithSamePromotionId()
        {
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Promoting);
            stagedPackage.ActivePromotionId = Guid.NewGuid();
            var previousSentDate = DateTime.UtcNow.AddMinutes(-61);
            stagedPackage.PromotionMessageSentDate = previousSentDate;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            SetupTransaction(repository, stagedPackage);
            StagingPromotionMessage message = null;
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .Callback<StagingPromotionMessage>(value => message = value)
                .Returns(Task.CompletedTask);
            var target = CreateService(repository, enqueuer);

            var result = await target.ResendPackageAsync(new User("owner"), stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Accepted, result);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.Equal(stagedPackage.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagedPackage, message.TargetType);
            Assert.Equal(stagedPackage.Key, message.TargetKey);
            Assert.True(stagedPackage.PromotionMessageSentDate > previousSentDate);
            repository.Verify(x => x.CommitChangesAsync(), Times.Once);
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

        [Fact]
        public async Task AcceptsAuthorizedReadyGroupBeforeSendingMessage()
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
            SetupTransaction(repository, group, stagedPackages, events);
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
            Assert.All(stagedPackages, stagedPackage =>
            {
                Assert.Equal(group.ActivePromotionId, stagedPackage.ActivePromotionId);
                Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            });
            Assert.Equal(new[] { "Transaction", "Commit:Promoting", "Send" }, events);
        }

        [Fact]
        public async Task ResendsStalledGroupWithSamePromotionId()
        {
            var group = CreateStagingGroup();
            group.ActivePromotionId = Guid.NewGuid();
            var previousSentDate = DateTime.UtcNow.AddMinutes(-61);
            group.PromotionMessageSentDate = previousSentDate;
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Promoting, group: group);
            stagedPackage.ActivePromotionId = group.ActivePromotionId;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            SetupTransaction(repository, group, new[] { stagedPackage });
            StagingPromotionMessage message = null;
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .Callback<StagingPromotionMessage>(value => message = value)
                .Returns(Task.CompletedTask);
            var target = CreateService(repository, enqueuer);

            var result = await target.ResendGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.Accepted, result);
            Assert.Equal(group.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagingGroup, message.TargetType);
            Assert.Equal(group.Key, message.TargetKey);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.Equal(group.ActivePromotionId, stagedPackage.ActivePromotionId);
            Assert.True(group.PromotionMessageSentDate > previousSentDate);
            repository.Verify(x => x.CommitChangesAsync(), Times.Once);
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
            SetupTransaction(repository, group, stagedPackages);
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
            SetupTransaction(repository, group, stagedPackages);
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
            SetupTransaction(repository, group, stagedPackages);
            repository
                .Setup(x => x.CommitChangesAsync())
                .ThrowsAsync(new DbUpdateConcurrencyException());
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1 }, group);

            Assert.Equal(StagingGroupPromotionResult.Conflict, result);
            Assert.Null(group.ActivePromotionId);
            Assert.Null(stagedPackages[0].ActivePromotionId);
            Assert.Equal(StagedPackageStatus.Ready, stagedPackages[0].Status);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task RollsBackGroupAcceptanceWhenSendingFails()
        {
            var group = CreateStagingGroup();
            var stagedPackages = new[] { CreateStagedPackage(StagedPackageStatus.Ready, key: 456, group) };
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(stagedPackages.AsQueryable());
            SetupTransaction(repository, group, stagedPackages);
            repository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
            var expectedException = new InvalidOperationException("Send failed.");
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            enqueuer
                .Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .ThrowsAsync(expectedException);
            var target = CreateService(repository, enqueuer);

            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.PromoteGroupAsync(new User("owner") { Key = 1 }, group));

            Assert.Same(expectedException, actualException);
            Assert.Null(group.ActivePromotionId);
            Assert.Null(stagedPackages[0].ActivePromotionId);
            Assert.Equal(StagedPackageStatus.Ready, stagedPackages[0].Status);
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
                    var originalSentDate = stagedPackage.PromotionMessageSentDate;

                    try
                    {
                        await action();
                    }
                    catch
                    {
                        stagedPackage.ActivePromotionId = originalPromotionId;
                        stagedPackage.Status = originalStatus;
                        stagedPackage.PromotionMessageSentDate = originalSentDate;
                        throw;
                    }
                });
        }

        private static void SetupTransaction(
            Mock<IEntityRepository<StagedPackage>> repository,
            StagingGroup group,
            IReadOnlyList<StagedPackage> stagedPackages,
            List<string> events = null)
        {
            repository
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(async action =>
                {
                    events?.Add("Transaction");
                    var originalGroupPromotionId = group.ActivePromotionId;
                    var originalGroupSentDate = group.PromotionMessageSentDate;
                    var originalPackages = stagedPackages
                        .Select(stagedPackage => new
                        {
                            Package = stagedPackage,
                            stagedPackage.ActivePromotionId,
                            stagedPackage.Status,
                        })
                        .ToList();

                    try
                    {
                        await action();
                    }
                    catch
                    {
                        group.ActivePromotionId = originalGroupPromotionId;
                        group.PromotionMessageSentDate = originalGroupSentDate;
                        foreach (var originalPackage in originalPackages)
                        {
                            originalPackage.Package.ActivePromotionId = originalPackage.ActivePromotionId;
                            originalPackage.Package.Status = originalPackage.Status;
                        }

                        throw;
                    }
                });
        }
    }
}

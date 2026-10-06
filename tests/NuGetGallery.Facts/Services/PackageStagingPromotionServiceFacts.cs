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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CommitsAuthorizedReadyPackageBeforeSendingMessage(bool includesSymbols)
        {
            var events = new List<string>();
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Ready);
            var symbols = includesSymbols ? CreateStagedSymbols(stagedPackage) : null;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            var saveCompleted = new TaskCompletionSource<bool>();
            repository
                .Setup(x => x.CommitChangesAsync())
                .Callback(() =>
                {
                    events.Add($"Commit:{stagedPackage.Status}");
                    if (symbols != null)
                    {
                        Assert.Equal(StagedPackageStatus.Promoting, symbols.Status);
                        Assert.Equal(stagedPackage.ActivePromotionId, symbols.ActivePromotionId);
                    }
                })
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
            var target = CreateService(repository, enqueuer, stagedSymbols: symbols == null ? null : new[] { symbols });

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
            if (symbols != null)
            {
                Assert.Equal(StagedPackageStatus.Promoting, symbols.Status);
                Assert.Equal(stagedPackage.ActivePromotionId, symbols.ActivePromotionId);
                Assert.Null(symbols.PromotionMessageSentDate);
            }
        }

        [Theory]
        [InlineData(StagedPackageStatus.Validating, true)]
        [InlineData(StagedPackageStatus.PromotionFailed, true)]
        [InlineData(StagedPackageStatus.Ready, false)]
        public async Task LeavesIneligibleSymbolsOutsideParentPromotion(StagedPackageStatus status, bool authorized)
        {
            var package = CreateStagedPackage(StagedPackageStatus.Ready);
            var symbols = CreateStagedSymbols(package);
            symbols.Status = status;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer, stagedSymbols: new[] { symbols }, authorizedSymbols: authorized);

            Assert.Equal(PackageStagingPromotionResult.Accepted, await target.PromotePackageAsync(package.StagedPackageIdentity.Owner, package));

            Assert.Equal(status, symbols.Status);
            Assert.Null(symbols.ActivePromotionId);
            Assert.Equal(StagedPackageStatus.Promoting, package.Status);
            enqueuer.Verify(x => x.SendMessageAsync(It.Is<StagingPromotionMessage>(message =>
                message.TargetType == StagingPromotionTargetType.StagedPackage && message.TargetKey == package.Key)), Times.Once);
        }

        [Fact]
        public async Task ResendsStalledPackageWithSamePromotionId()
        {
            var events = new List<string>();
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Promoting);
            stagedPackage.ActivePromotionId = Guid.NewGuid();
            var previousSentDate = DateTime.UtcNow.AddMinutes(-61);
            stagedPackage.PromotionMessageSentDate = previousSentDate;
            stagedPackage.StagedPackageIdentity.Package.PackageRegistration.Owners.Clear();
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
            var symbols = CreateStagedSymbols(stagedPackage);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository
                .Setup(x => x.CommitChangesAsync())
                .ThrowsAsync(new DbUpdateConcurrencyException());
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer, stagedSymbols: new[] { symbols });

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AcceptsReadySymbolsWithTheirStagedOrPublishedParent(bool hasStagedParent)
        {
            var group = CreateStagingGroup();
            var parent = CreateStagedPackage(StagedPackageStatus.Ready, group: group);
            var symbols = CreateStagedSymbols(parent);
            if (!hasStagedParent)
            {
                parent.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
                parent.StagedPackageIdentity.CurrentStagedPackageKey = null;
                parent.StagedPackageIdentity.CurrentStagedPackage = null;
            }

            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns((hasStagedParent ? new[] { parent } : Array.Empty<StagedPackage>()).AsQueryable());
            var committed = new TaskCompletionSource<bool>();
            repository.Setup(x => x.CommitChangesAsync()).Callback(() =>
            {
                Assert.Equal(StagedPackageStatus.Promoting, symbols.Status);
                Assert.Equal(group.ActivePromotionId, symbols.ActivePromotionId);
            }).Returns(committed.Task);
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer, stagedSymbols: new[] { symbols });

            var promotion = target.PromoteGroupAsync(group.Owner, group);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            committed.SetResult(true);
            var result = await promotion;

            Assert.Equal(StagingGroupPromotionResult.Accepted, result);
            Assert.NotNull(group.ActivePromotionId);
            Assert.Null(symbols.PromotionMessageSentDate);
            if (hasStagedParent)
            {
                Assert.Equal(StagedPackageStatus.Promoting, parent.Status);
                Assert.Equal(group.ActivePromotionId, parent.ActivePromotionId);
            }

            enqueuer.Verify(x => x.SendMessageAsync(It.Is<StagingPromotionMessage>(message =>
                message.TargetType == StagingPromotionTargetType.StagingGroup && message.TargetKey == group.Key && message.PromotionId == group.ActivePromotionId)), Times.Once);
        }

        [Theory]
        [InlineData("failed-promotion")]
        [InlineData("unauthorized")]
        [InlineData("deleted-parent")]
        public async Task RejectsTheWholeGroupWhenSymbolsAreBlocked(string blocker)
        {
            var group = CreateStagingGroup();
            var parent = CreateStagedPackage(StagedPackageStatus.Ready, group: group);
            var symbols = CreateStagedSymbols(parent);
            if (blocker == "failed-promotion")
            {
                symbols.Status = StagedPackageStatus.PromotionFailed;
            }
            else if (blocker == "deleted-parent")
            {
                parent.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Deleted;
            }

            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(new[] { parent }.AsQueryable());
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer, stagedSymbols: new[] { symbols }, authorizedSymbols: blocker != "unauthorized");

            var result = await target.PromoteGroupAsync(group.Owner, group);

            Assert.Equal(blocker == "unauthorized" ? StagingGroupPromotionResult.Unauthorized : StagingGroupPromotionResult.NotReady, result);
            Assert.Null(group.ActivePromotionId);
            Assert.Null(parent.ActivePromotionId);
            Assert.Null(symbols.ActivePromotionId);
            Assert.Equal(StagedPackageStatus.Ready, parent.Status);
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task RejectsPackageExpiringWhileReadingSymbols()
        {
            var attempt = CreateStagedPackage(StagedPackageStatus.Ready);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var symbols = new Mock<IEntityRepository<StagedSymbolPackage>>();
            var target = new PackageStagingPromotionService(
                Mock.Of<IPackageStagingAuthorizationService>(x => x.CanManage(It.IsAny<User>(), attempt) == true),
                enqueuer.Object,
                repository.Object,
                symbols.Object);
            symbols.Setup(x => x.GetAll()).Returns(() =>
            {
                attempt.ExpirationDate = DateTime.UtcNow;
                return Array.Empty<StagedSymbolPackage>().AsQueryable();
            });

            Assert.Equal(PackageStagingPromotionResult.NotReady, await target.PromotePackageAsync(attempt.StagedPackageIdentity.Owner, attempt));

            Assert.Equal(StagedPackageStatus.Ready, attempt.Status);
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task RejectsGroupExpiringWhileReadingMembers()
        {
            var owner = new User("owner") { Key = 1 };
            var group = new StagingGroup { Key = 10, Owner = owner, OwnerKey = owner.Key };
            var attempt = CreateStagedPackage(StagedPackageStatus.Ready, group: group);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(() =>
            {
                group.ExpirationDate = DateTime.UtcNow;
                return new[] { attempt }.AsQueryable();
            });
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer);

            Assert.Equal(StagingGroupPromotionResult.NotReady, await target.PromoteGroupAsync(owner, group));

            Assert.Null(group.ActivePromotionId);
            Assert.Equal(StagedPackageStatus.Ready, attempt.Status);
            repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task OwnershipLossBlocksAcceptanceWithoutMutationAndRestorationAllowsPromotion(bool grouped, bool symbolOnly)
        {
            var group = grouped ? CreateStagingGroup() : null;
            var parent = CreateStagedPackage(StagedPackageStatus.Ready, group: group);
            var identity = parent.StagedPackageIdentity;
            var deadline = DateTime.UtcNow.AddDays(3);
            parent.ExpirationDate = deadline;
            if (group != null)
            {
                group.ExpirationDate = deadline;
            }

            var symbols = grouped ? CreateStagedSymbols(parent) : null;
            if (symbolOnly)
            {
                identity.Package.PackageStatusKey = PackageStatus.Available;
                identity.CurrentStagedPackageKey = null;
                identity.CurrentStagedPackage = null;
            }

            var packages = symbolOnly ? Array.Empty<StagedPackage>() : new[] { parent };
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(service => service.GetAll()).Returns(packages.AsQueryable());
            repository.Setup(service => service.CommitChangesAsync()).Returns(Task.CompletedTask);
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer, stagedSymbols: symbols == null ? null : new[] { symbols });
            identity.Package.PackageRegistration.Owners.Clear();

            if (grouped)
            {
                Assert.Equal(StagingGroupPromotionResult.NotReady, await target.PromoteGroupAsync(identity.Owner, group));
                Assert.False(StagingGroupResponse.FromGroup(group, packages, deadline, "management", new[] { symbols }).CanPromote);
                Assert.Null(group.ActivePromotionId);
                Assert.Equal(StagedPackageStatus.Ready, symbols.Status);
            }
            else
            {
                Assert.Equal(PackageStagingPromotionResult.NotReady, await target.PromotePackageAsync(identity.Owner, parent));
                var response = StagingArtifactResponse.FromPackage(parent, deadline, "management");
                Assert.False(response.CanPromote);
                Assert.Equal("RegistrationOwnershipLost", Assert.Single(response.Blockers).Code);
                Assert.Null(response.Group);
            }

            Assert.Equal(StagedPackageStatus.Ready, parent.Status);
            Assert.Null(parent.ActivePromotionId);
            Assert.Equal(deadline, parent.ExpirationDate);
            repository.Verify(service => service.CommitChangesAsync(), Times.Never);
            enqueuer.Verify(service => service.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);

            identity.Package.PackageRegistration.Owners.Add(identity.Owner);
            if (grouped)
            {
                Assert.True(StagingGroupResponse.FromGroup(group, packages, deadline, "management", new[] { symbols }).CanPromote);
                Assert.Equal(StagingGroupPromotionResult.Accepted, await target.PromoteGroupAsync(identity.Owner, group));
                Assert.Equal(deadline, group.ExpirationDate);
            }
            else
            {
                Assert.True(StagingArtifactResponse.FromPackage(parent, deadline, "management").CanPromote);
                Assert.Equal(PackageStagingPromotionResult.Accepted, await target.PromotePackageAsync(identity.Owner, parent));
            }

            Assert.Equal(deadline, parent.ExpirationDate);
            repository.Verify(service => service.CommitChangesAsync(), Times.Once);
            enqueuer.Verify(service => service.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Once);
        }

        private static PackageStagingPromotionService CreateService(
            Mock<IEntityRepository<StagedPackage>> repository,
            Mock<IStagingPromotionMessageEnqueuer> enqueuer,
            bool authorized = true,
            int? authorizedPackageKey = null,
            IEnumerable<StagedSymbolPackage> stagedSymbols = null,
            bool authorizedSymbols = true)
        {
            var authorizationService = new Mock<IPackageStagingAuthorizationService>();
            authorizationService
                .Setup(x => x.CanManage(It.IsAny<User>(), It.IsAny<StagedPackage>()))
                .Returns<User, StagedPackage>((currentUser, stagedPackage) =>
                    authorized && (!authorizedPackageKey.HasValue || stagedPackage.Key == authorizedPackageKey.Value));
            authorizationService
                .Setup(x => x.GetEnabledOwners(It.IsAny<User>()))
                .Returns<User>(currentUser => new[] { currentUser });
            authorizationService.Setup(x => x.CanManage(It.IsAny<User>(), It.IsAny<StagedSymbolPackage>())).Returns(authorizedSymbols);

            return new PackageStagingPromotionService(
                authorizationService.Object,
                enqueuer.Object,
                repository.Object,
                Mock.Of<IEntityRepository<StagedSymbolPackage>>(x => x.GetAll() == (stagedSymbols ?? Array.Empty<StagedSymbolPackage>()).AsQueryable()));
        }

        private static StagedSymbolPackage CreateStagedSymbols(StagedPackage parent)
        {
            var identity = parent.StagedPackageIdentity;
            var symbols = new StagedSymbolPackage
            {
                Key = 1000,
                StagedPackageIdentityKey = identity.Key,
                StagedPackageIdentity = identity,
                SymbolPackage = new SymbolPackage { PackageKey = identity.Key, StatusKey = PackageStatus.Staged },
                Status = StagedPackageStatus.Ready,
            };
            identity.CurrentStagedSymbolPackageKey = symbols.Key;
            identity.CurrentStagedSymbolPackage = symbols;
            return symbols;
        }

        private static StagedPackage CreateStagedPackage(StagedPackageStatus status, int key = StagedPackageKey, StagingGroup group = null)
        {
            var owner = new User("owner") { Key = 1 };
            var registration = new PackageRegistration { Id = "PackageA" };
            registration.Owners.Add(owner);
            var package = new Package { Key = key + 1, PackageStatusKey = PackageStatus.Staged, PackageRegistration = registration };
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

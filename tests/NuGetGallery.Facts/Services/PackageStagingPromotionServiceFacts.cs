// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
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
        [InlineData("requester-locked")]
        [InlineData("requester-unconfirmed")]
        [InlineData("owner-locked")]
        [InlineData("owner-unconfirmed")]
        [InlineData("registration-locked")]
        [InlineData("ownership-lost")]
        public async Task PublishingRestrictionsRejectParentAndGroupRequestsWithoutAcceptingAnyWork(string restriction)
        {
            foreach (var grouped in new[] { false, true })
            {
                foreach (var resend in new[] { false, true })
                {
                    if (restriction == "ownership-lost" && !resend)
                    {
                        continue;
                    }

                    var requester = new User("member") { Key = 1, EmailAddress = "member@example.test" };
                    var group = grouped ? CreateStagingGroup() : null;
                    var parent = CreateStagedPackage(resend ? StagedPackageStatus.Promoting : StagedPackageStatus.Ready, group: group);
                    var symbols = CreateStagedSymbols(parent);
                    var promotionId = resend ? Guid.NewGuid() : (Guid?)null;
                    parent.ActivePromotionId = promotionId;
                    parent.PromotionMessageSentDate = resend ? DateTime.UtcNow.AddHours(-2) : (DateTime?)null;
                    symbols.Status = parent.Status;
                    symbols.ActivePromotionId = promotionId;
                    if (grouped)
                    {
                        group.ActivePromotionId = promotionId;
                        group.PromotionMessageSentDate = parent.PromotionMessageSentDate;
                    }

                    ApplyPublishingRestriction(restriction, requester, parent.StagedPackageIdentity);
                    var otherParent = CreateStagedPackage(parent.Status, key: 789, group: group);
                    var repository = new Mock<IEntityRepository<StagedPackage>>();
                    repository.Setup(service => service.GetAll()).Returns(new[] { otherParent, parent }.AsQueryable());
                    var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
                    var target = CreateService(repository, enqueuer, stagedSymbols: new[] { symbols });
                    if (grouped)
                    {
                        var result = resend
                            ? await target.ResendGroupAsync(requester, group)
                            : await target.PromoteGroupAsync(requester, group);
                        Assert.Equal(StagingGroupPromotionResult.NotReady, result);
                        Assert.Equal(promotionId, group.ActivePromotionId);
                    }
                    else
                    {
                        var result = resend
                            ? await target.ResendPackageAsync(requester, parent)
                            : await target.PromotePackageAsync(requester, parent);
                        Assert.Equal(PackageStagingPromotionResult.NotReady, result);
                    }

                    Assert.Equal(resend ? StagedPackageStatus.Promoting : StagedPackageStatus.Ready, parent.Status);
                    Assert.Equal(parent.Status, symbols.Status);
                    Assert.Equal(parent.Status, otherParent.Status);
                    Assert.Equal(promotionId, parent.ActivePromotionId);
                    Assert.Equal(promotionId, symbols.ActivePromotionId);
                    Assert.Null(otherParent.ActivePromotionId);
                    repository.Verify(service => service.CommitChangesAsync(), Times.Never);
                    enqueuer.Verify(service => service.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);

                    if (resend || restriction.StartsWith("requester-", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (grouped)
                    {
                        var groupResponse = StagingGroupResponse.FromGroup(group, new[] { otherParent, parent }, group.ExpirationDate, "management", new[] { symbols });
                        Assert.False(groupResponse.CanPromote);
                        Assert.Equal(StagingPublicationPolicy.GetBlocker(parent.StagedPackageIdentity).Code, Assert.Single(groupResponse.Blockers).Code);
                    }
                    else
                    {
                        var artifactResponse = StagingArtifactResponse.FromPackage(parent, "management");
                        Assert.False(artifactResponse.CanPromote);
                        Assert.Equal(StagingPublicationPolicy.GetBlocker(parent.StagedPackageIdentity).Code, Assert.Single(artifactResponse.Blockers).Code);
                    }
                }
            }
        }

        private static void ApplyPublishingRestriction(string restriction, User requester, StagedPackageIdentity identity)
        {
            switch (restriction)
            {
                case "requester-locked":
                    requester.UserStatusKey = UserStatus.Locked;
                    break;
                case "requester-unconfirmed":
                    requester.EmailAddress = null;
                    break;
                case "owner-locked":
                    identity.Owner.UserStatusKey = UserStatus.Locked;
                    break;
                case "owner-unconfirmed":
                    identity.Owner.EmailAddress = null;
                    break;
                case "registration-locked":
                    identity.Package.PackageRegistration.IsLocked = true;
                    break;
                case "ownership-lost":
                    identity.Package.PackageRegistration.Owners.Clear();
                    break;
                default:
                    throw new ArgumentException("Unknown publishing restriction.", nameof(restriction));
            }
        }

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
                    Assert.NotNull(stagedPackage.PromotionAcceptedAt);
                    Assert.Equal(stagedPackage.PromotionMessageSentDate, stagedPackage.PromotionAcceptedAt);
                    if (symbols != null)
                    {
                        Assert.Equal(StagedPackageStatus.Promoting, symbols.Status);
                        Assert.Equal(stagedPackage.ActivePromotionId, symbols.ActivePromotionId);
                        Assert.Equal(stagedPackage.PromotionAcceptedAt, symbols.PromotionAcceptedAt);
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
            var telemetry = new Mock<ITelemetryService>();
            telemetry.Setup(service => service.TrackStagingPromotionAccepted(It.IsAny<Guid>(), stagedPackage.StagedPackageIdentity.Package, It.IsAny<bool>()))
                .Callback<Guid, Package, bool>((promotionId, package, isSymbols) =>
                {
                    Assert.Equal(stagedPackage.ActivePromotionId, promotionId);
                    events.Add(isSymbols ? "Accepted:Symbols" : "Accepted:Package");
                });
            var target = CreateService(repository, enqueuer, stagedSymbols: symbols == null ? null : new[] { symbols }, telemetry: telemetry);

            var promotion = target.PromotePackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            telemetry.Verify(service => service.TrackStagingPromotionAccepted(It.IsAny<Guid>(), It.IsAny<Package>(), It.IsAny<bool>()), Times.Never);
            saveCompleted.SetResult(true);
            var result = await promotion;

            Assert.Equal(PackageStagingPromotionResult.Accepted, result);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.Equal(stagedPackage.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagedPackage, message.TargetType);
            Assert.Equal(StagedPackageKey, message.TargetKey);
            var expectedEvents = new List<string> { "Commit:Promoting", "Accepted:Package" };
            if (includesSymbols)
            {
                expectedEvents.Add("Accepted:Symbols");
            }

            expectedEvents.Add("Send");
            Assert.Equal(expectedEvents, events);
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
            var acceptedAt = DateTime.UtcNow.AddHours(-2);
            stagedPackage.PromotionAcceptedAt = acceptedAt;
            var previousSentDate = DateTime.UtcNow.AddMinutes(-61);
            stagedPackage.PromotionMessageSentDate = previousSentDate;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(new[] { stagedPackage }.AsQueryable());
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
            var telemetry = new Mock<ITelemetryService>();
            var target = CreateService(repository, enqueuer, telemetry: telemetry);

            var result = await target.ResendPackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Accepted, result);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.Equal(stagedPackage.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagedPackage, message.TargetType);
            Assert.Equal(stagedPackage.Key, message.TargetKey);
            Assert.True(stagedPackage.PromotionMessageSentDate > previousSentDate);
            Assert.Equal(acceptedAt, stagedPackage.PromotionAcceptedAt);
            Assert.Equal(new[] { "Commit", "Send" }, events);
            telemetry.Verify(service => service.TrackStagingPromotionAccepted(It.IsAny<Guid>(), It.IsAny<Package>(), It.IsAny<bool>()), Times.Never);
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

            var result = await target.ResendPackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage);

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

            var result = await target.ResendPackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage);

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

            var result = await target.PromotePackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage);

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

            var result = await target.PromotePackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage);

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
            var telemetry = new Mock<ITelemetryService>();
            var target = CreateService(repository, enqueuer, stagedSymbols: new[] { symbols }, telemetry: telemetry);

            var result = await target.PromotePackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.Conflict, result);
            telemetry.Verify(service => service.TrackStagingPromotionAccepted(It.IsAny<Guid>(), It.IsAny<Package>(), It.IsAny<bool>()), Times.Never);
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
            var telemetry = new Mock<ITelemetryService>();
            var target = CreateService(repository, enqueuer, telemetry: telemetry);

            var result = await target.PromotePackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage);

            Assert.Equal(PackageStagingPromotionResult.DispatchFailed, result);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.NotNull(stagedPackage.ActivePromotionId);
            Assert.True(StagingPromotionResendPolicy.IsDue(stagedPackage.PromotionMessageSentDate));
            var acceptedAt = stagedPackage.PromotionAcceptedAt;
            Assert.NotNull(acceptedAt);
            repository.Verify(x => x.CommitChangesAsync(), Times.Exactly(2));

            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).Returns(Task.CompletedTask);
            Assert.Equal(PackageStagingPromotionResult.Accepted, await target.ResendPackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage));
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.False(StagingPromotionResendPolicy.IsDue(stagedPackage.PromotionMessageSentDate));
            Assert.Equal(acceptedAt, stagedPackage.PromotionAcceptedAt);
            telemetry.Verify(service => service.TrackStagingPromotionAccepted(stagedPackage.ActivePromotionId.Value, stagedPackage.StagedPackageIdentity.Package, false), Times.Once);
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

            var result = await target.PromotePackageAsync(new User("owner") { EmailAddress = "owner@example.test" }, stagedPackage);

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
            var telemetry = new Mock<ITelemetryService>();
            telemetry.Setup(service => service.TrackStagingPromotionAccepted(It.IsAny<Guid>(), It.IsAny<Package>(), false))
                .Callback<Guid, Package, bool>((promotionId, package, symbols) =>
                {
                    Assert.Equal(group.ActivePromotionId, promotionId);
                    Assert.Contains(stagedPackages, attempt => attempt.StagedPackageIdentity.Package == package);
                    events.Add("Accepted:Package");
                });
            var target = CreateService(repository, enqueuer, telemetry: telemetry);

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1, EmailAddress = "owner@example.test" }, group);

            Assert.Equal(StagingGroupPromotionResult.Accepted, result);
            Assert.Equal(group.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagingGroup, message.TargetType);
            Assert.Equal(group.Key, message.TargetKey);
            Assert.Equal(0, group.MutationRevision);
            Assert.All(stagedPackages, stagedPackage =>
            {
                Assert.Equal(group.ActivePromotionId, stagedPackage.ActivePromotionId);
                Assert.NotNull(group.PromotionAcceptedAt);
                Assert.Equal(group.PromotionAcceptedAt, stagedPackage.PromotionAcceptedAt);
                Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            });
            Assert.Equal(new[] { "Commit:Promoting", "Accepted:Package", "Accepted:Package", "Send" }, events);
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(0, 1)]
        [InlineData(101, 101)]
        [InlineData(500, 500)]
        public async Task CommitsAllAcceptedArtifactsTogetherBeforeDispatch(int parentCount, int symbolCount)
        {
            var group = CreateStagingGroup();
            var parents = new List<StagedPackage>();
            var symbols = new List<StagedSymbolPackage>();
            for (var index = 0; index < Math.Max(parentCount, symbolCount); index++)
            {
                var parent = CreateStagedPackage(StagedPackageStatus.Ready, key: index + 1, group);
                parent.RowVersion = BitConverter.GetBytes((long)index + 1);
                if (index < parentCount)
                {
                    parents.Add(parent);
                }
                else
                {
                    parent.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
                    parent.StagedPackageIdentity.CurrentStagedPackageKey = null;
                    parent.StagedPackageIdentity.CurrentStagedPackage = null;
                }

                if (index < symbolCount)
                {
                    var attempt = CreateStagedSymbols(parent);
                    attempt.Key = 10000 + index;
                    attempt.RowVersion = BitConverter.GetBytes((long)10000 + index);
                    attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey = attempt.Key;
                    symbols.Add(attempt);
                }
            }

            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(parents.AsQueryable());
            var commitCompleted = new TaskCompletionSource<bool>();
            repository.Setup(x => x.CommitChangesAsync()).Returns(commitCompleted.Task);
            var writesCompleted = new TaskCompletionSource<bool>();
            var transactionCompleted = new TaskCompletionSource<bool>();
            var observedParents = new List<int>();
            var observedSymbols = new List<int>();
            var database = new Mock<IDatabase>();
            database.Setup(x => x.ExecuteSqlCommandAsync(It.IsAny<string>(), It.IsAny<object[]>()))
                .Returns<string, object[]>((sql, values) =>
                {
                    var parameters = values.Cast<SqlParameter>().ToList();
                    Assert.Equal(group.ActivePromotionId, parameters.Single(parameter => parameter.ParameterName == "@promotionId").Value);
                    var acceptedAt = parameters.Single(parameter => parameter.ParameterName == "@acceptedAt");
                    Assert.Equal(SqlDbType.DateTime2, acceptedAt.SqlDbType);
                    Assert.Equal(group.PromotionAcceptedAt, acceptedAt.Value);
                    Assert.Equal((int)StagedPackageStatus.Promoting, parameters.Single(parameter => parameter.ParameterName == "@status").Value);
                    var keys = parameters.Where(parameter => parameter.ParameterName.StartsWith("@key", StringComparison.Ordinal)).ToList();
                    Assert.InRange(keys.Count, 1, 100);
                    Assert.Equal(3 + keys.Count * 2, parameters.Count);
                    foreach (var key in keys)
                    {
                        var rowVersionName = key.ParameterName.Replace("@key", "@rowVersion");
                        var rowVersion = parameters.Single(parameter => parameter.ParameterName == rowVersionName);
                        Assert.Equal(BitConverter.GetBytes((long)(int)key.Value), Assert.IsType<byte[]>(rowVersion.Value));
                    }

                    if (sql.Contains("[StagedSymbolPackages]"))
                    {
                        observedSymbols.AddRange(keys.Select(parameter => (int)parameter.Value));
                        Assert.Contains("[PromotionMessageSentDate] = NULL", sql);
                    }
                    else
                    {
                        observedParents.AddRange(keys.Select(parameter => (int)parameter.Value));
                        Assert.DoesNotContain("[PromotionMessageSentDate]", sql);
                    }

                    return Task.FromResult(keys.Count);
                });
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var telemetry = new Mock<ITelemetryService>();
            var target = CreateService(repository, enqueuer, stagedSymbols: symbols, telemetry: telemetry, database: database);
            repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(async action =>
                {
                    await action();
                    writesCompleted.SetResult(true);
                    await transactionCompleted.Task;
                });

            var promotion = target.PromoteGroupAsync(group.Owner, group);
            database.Verify(x => x.ExecuteSqlCommandAsync(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            telemetry.Verify(x => x.TrackStagingPromotionAccepted(It.IsAny<Guid>(), It.IsAny<Package>(), It.IsAny<bool>()), Times.Never);
            commitCompleted.SetResult(true);
            Assert.Same(writesCompleted.Task, await Task.WhenAny(writesCompleted.Task, promotion));
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            telemetry.Verify(x => x.TrackStagingPromotionAccepted(It.IsAny<Guid>(), It.IsAny<Package>(), It.IsAny<bool>()), Times.Never);
            transactionCompleted.SetResult(true);

            Assert.Equal(StagingGroupPromotionResult.Accepted, await promotion);
            Assert.Equal(parents.Select(attempt => attempt.Key), observedParents);
            Assert.Equal(symbols.Select(attempt => attempt.Key), observedSymbols);
            database.Verify(x => x.ExecuteSqlCommandAsync(It.IsAny<string>(), It.IsAny<object[]>()), Times.Exactly((parentCount + 99) / 100 + (symbolCount + 99) / 100));
            repository.Verify(x => x.CommitChangesAsync(), Times.Once);
            enqueuer.Verify(x => x.SendMessageAsync(It.Is<StagingPromotionMessage>(message => message.TargetType == StagingPromotionTargetType.StagingGroup && message.PromotionId == group.ActivePromotionId)), Times.Once);
        }

        [Fact]
        public async Task ResendsStalledGroupWithSamePromotionId()
        {
            var events = new List<string>();
            var group = CreateStagingGroup();
            group.ActivePromotionId = Guid.NewGuid();
            var acceptedAt = DateTime.UtcNow.AddHours(-2);
            group.PromotionAcceptedAt = acceptedAt;
            group.MutationRevision = 3;
            var previousSentDate = DateTime.UtcNow.AddMinutes(-61);
            group.PromotionMessageSentDate = previousSentDate;
            var stagedPackage = CreateStagedPackage(StagedPackageStatus.Promoting, group: group);
            stagedPackage.ActivePromotionId = group.ActivePromotionId;
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(new[] { stagedPackage }.AsQueryable());
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
            var telemetry = new Mock<ITelemetryService>();
            var target = CreateService(repository, enqueuer, telemetry: telemetry);

            var result = await target.ResendGroupAsync(new User("owner") { Key = 1, EmailAddress = "owner@example.test" }, group);

            Assert.Equal(StagingGroupPromotionResult.Accepted, result);
            Assert.Equal(group.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagingGroup, message.TargetType);
            Assert.Equal(group.Key, message.TargetKey);
            Assert.Equal(3, group.MutationRevision);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackage.Status);
            Assert.Equal(group.ActivePromotionId, stagedPackage.ActivePromotionId);
            Assert.True(group.PromotionMessageSentDate > previousSentDate);
            Assert.Equal(acceptedAt, group.PromotionAcceptedAt);
            Assert.Equal(new[] { "Commit", "Send" }, events);
            telemetry.Verify(service => service.TrackStagingPromotionAccepted(It.IsAny<Guid>(), It.IsAny<Package>(), It.IsAny<bool>()), Times.Never);
            repository.Verify(x => x.CommitChangesAsync(), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DoesNotCommitOrDispatchWhenAnAcceptanceBatchConflicts(bool symbolConflict)
        {
            var group = CreateStagingGroup();
            var parent = CreateStagedPackage(StagedPackageStatus.Ready, group: group);
            var symbols = CreateStagedSymbols(parent);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(new[] { parent }.AsQueryable());
            repository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
            var database = new Mock<IDatabase>();
            database.Setup(x => x.ExecuteSqlCommandAsync(It.IsAny<string>(), It.IsAny<object[]>()))
                .Returns<string, object[]>((sql, parameters) => Task.FromResult(sql.Contains("[StagedSymbolPackages]") == symbolConflict ? 0 : 1));
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var telemetry = new Mock<ITelemetryService>();
            var target = CreateService(repository, enqueuer, stagedSymbols: new[] { symbols }, telemetry: telemetry, database: database);
            var transactionCommitted = false;
            repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(async action =>
                {
                    await action();
                    transactionCommitted = true;
                });

            Assert.Equal(StagingGroupPromotionResult.Conflict, await target.PromoteGroupAsync(group.Owner, group));

            Assert.False(transactionCommitted);
            telemetry.Verify(x => x.TrackStagingPromotionAccepted(It.IsAny<Guid>(), It.IsAny<Package>(), It.IsAny<bool>()), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task ReadOnlyGroupSavePreventsBatchWritesAndDispatch()
        {
            var group = CreateStagingGroup();
            var parent = CreateStagedPackage(StagedPackageStatus.Ready, group: group);
            var repository = new Mock<IEntityRepository<StagedPackage>>();
            repository.Setup(x => x.GetAll()).Returns(new[] { parent }.AsQueryable());
            repository.Setup(x => x.CommitChangesAsync()).ThrowsAsync(new ReadOnlyModeException("Gallery is read-only."));
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            var enqueuer = new Mock<IStagingPromotionMessageEnqueuer>();
            var target = CreateService(repository, enqueuer, database: database);

            await Assert.ThrowsAsync<ReadOnlyModeException>(() => target.PromoteGroupAsync(group.Owner, group));

            database.Verify(x => x.ExecuteSqlCommandAsync(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
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

            var result = await target.ResendGroupAsync(new User("owner") { Key = 1, EmailAddress = "owner@example.test" }, group);

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

            var result = await target.ResendGroupAsync(new User("owner") { Key = 1, EmailAddress = "owner@example.test" }, group);

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

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1, EmailAddress = "owner@example.test" }, group);

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

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1, EmailAddress = "owner@example.test" }, group);

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

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1, EmailAddress = "owner@example.test" }, group);

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

            var result = await target.PromoteGroupAsync(new User("owner") { Key = 1, EmailAddress = "owner@example.test" }, group);

            Assert.Equal(StagingGroupPromotionResult.DispatchFailed, result);
            Assert.NotNull(group.ActivePromotionId);
            Assert.Equal(group.ActivePromotionId, stagedPackages[0].ActivePromotionId);
            Assert.Equal(StagedPackageStatus.Promoting, stagedPackages[0].Status);
            Assert.True(StagingPromotionResendPolicy.IsDue(group.PromotionMessageSentDate));
            repository.Verify(x => x.CommitChangesAsync(), Times.Exactly(2));

            enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).Returns(Task.CompletedTask);
            Assert.Equal(StagingGroupPromotionResult.Accepted, await target.ResendGroupAsync(new User("owner") { Key = 1, EmailAddress = "owner@example.test" }, group));
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
            var telemetry = new Mock<ITelemetryService>();
            var target = CreateService(repository, enqueuer, stagedSymbols: new[] { symbols }, telemetry: telemetry);

            var promotion = target.PromoteGroupAsync(group.Owner, group);
            enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            telemetry.Verify(service => service.TrackStagingPromotionAccepted(It.IsAny<Guid>(), It.IsAny<Package>(), It.IsAny<bool>()), Times.Never);
            committed.SetResult(true);
            var result = await promotion;

            Assert.Equal(StagingGroupPromotionResult.Accepted, result);
            Assert.NotNull(group.ActivePromotionId);
            telemetry.Verify(service => service.TrackStagingPromotionAccepted(group.ActivePromotionId.Value, parent.StagedPackageIdentity.Package, true), Times.Once);
            Assert.NotNull(group.PromotionAcceptedAt);
            Assert.Equal(group.PromotionAcceptedAt, symbols.PromotionAcceptedAt);
            telemetry.Verify(service => service.TrackStagingPromotionAccepted(group.ActivePromotionId.Value, parent.StagedPackageIdentity.Package, false),
                hasStagedParent ? Times.Once() : Times.Never());
            Assert.Null(symbols.PromotionMessageSentDate);
            if (hasStagedParent)
            {
                Assert.Equal(StagedPackageStatus.Promoting, parent.Status);
                Assert.Equal(group.ActivePromotionId, parent.ActivePromotionId);
                Assert.Equal(group.PromotionAcceptedAt, parent.PromotionAcceptedAt);
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
                symbols.Object,
                Mock.Of<ITelemetryService>(),
                Mock.Of<IEntitiesContext>());
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
            var owner = new User("owner") { Key = 1, EmailAddress = "owner@example.test" };
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
                var response = StagingGroupResponse.FromGroup(group, packages, deadline, "management", new[] { symbols });
                Assert.False(response.CanPromote);
                Assert.Equal("RegistrationOwnershipLost", Assert.Single(response.Blockers).Code);
                Assert.Null(group.ActivePromotionId);
                Assert.Equal(StagedPackageStatus.Ready, symbols.Status);
            }
            else
            {
                Assert.Equal(PackageStagingPromotionResult.NotReady, await target.PromotePackageAsync(identity.Owner, parent));
                var response = StagingArtifactResponse.FromPackage(parent, "management");
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
                var response = StagingGroupResponse.FromGroup(group, packages, deadline, "management", new[] { symbols });
                Assert.True(response.CanPromote);
                Assert.Empty(response.Blockers);
                Assert.Equal(StagingGroupPromotionResult.Accepted, await target.PromoteGroupAsync(identity.Owner, group));
                Assert.Equal(deadline, group.ExpirationDate);
            }
            else
            {
                Assert.True(StagingArtifactResponse.FromPackage(parent, "management").CanPromote);
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
            bool authorizedSymbols = true,
            Mock<ITelemetryService> telemetry = null,
            Mock<IDatabase> database = null)
        {
            repository.SetReturnsDefault(Array.Empty<StagedPackage>().AsQueryable());
            var authorizationService = new Mock<IPackageStagingAuthorizationService>();
            authorizationService
                .Setup(x => x.CanManage(It.IsAny<User>(), It.IsAny<StagedPackage>()))
                .Returns<User, StagedPackage>((currentUser, stagedPackage) =>
                    authorized && (!authorizedPackageKey.HasValue || stagedPackage.Key == authorizedPackageKey.Value));
            authorizationService
                .Setup(x => x.GetEnabledOwners(It.IsAny<User>()))
                .Returns<User>(currentUser => new[] { currentUser });
            authorizationService.Setup(x => x.CanManage(It.IsAny<User>(), It.IsAny<StagedSymbolPackage>())).Returns(authorizedSymbols);
            if (database == null)
            {
                database = new Mock<IDatabase>();
                database.Setup(x => x.ExecuteSqlCommandAsync(It.IsAny<string>(), It.IsAny<object[]>()))
                    .Returns<string, object[]>((sql, parameters) => Task.FromResult((parameters.Length - 3) / 2));
            }
            repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>())).Returns<Func<Task>>(action => action());

            return new PackageStagingPromotionService(
                authorizationService.Object,
                enqueuer.Object,
                repository.Object,
                Mock.Of<IEntityRepository<StagedSymbolPackage>>(x => x.GetAll() == (stagedSymbols ?? Array.Empty<StagedSymbolPackage>()).AsQueryable()),
                telemetry?.Object ?? Mock.Of<ITelemetryService>(),
                Mock.Of<IEntitiesContext>(context => context.GetDatabase() == database.Object));
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
            var owner = group?.Owner ?? new User("owner") { Key = 1, EmailAddress = "owner@example.test" };
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
            var owner = new User("owner") { Key = 1, EmailAddress = "owner@example.test" };
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

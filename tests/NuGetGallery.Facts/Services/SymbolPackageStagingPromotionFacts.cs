// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using Xunit;

namespace NuGetGallery
{
    public class SymbolPackageStagingPromotionFacts
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CommitsExactSymbolAttemptBeforeDispatchWithoutPublishingItsEntities(bool hasPublicSymbols)
        {
            var fixture = new Fixture();
            if (hasPublicSymbols)
            {
                fixture.Attempt.StagedPackageIdentity.Package.SymbolPackages.Add(new SymbolPackage { StatusKey = PackageStatus.Available });
            }

            var response = StagingArtifactResponse.FromSymbolPackage(fixture.Attempt, DateTime.UtcNow.AddDays(30), "management");
            Assert.True(response.CanPromote);
            Assert.Empty(response.Blockers);
            var committed = new TaskCompletionSource<bool>();
            fixture.Repository.Setup(x => x.CommitChangesAsync()).Returns(committed.Task);
            StagingPromotionMessage message = null;
            fixture.Enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()))
                .Callback<StagingPromotionMessage>(value => message = value)
                .Returns(Task.CompletedTask);

            var promotion = fixture.Service.PromoteSymbolPackageAsync(fixture.Owner, fixture.Attempt);
            fixture.Enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            committed.SetResult(true);

            Assert.Equal(PackageStagingPromotionResult.Accepted, await promotion);
            Assert.Equal(StagedPackageStatus.Promoting, fixture.Attempt.Status);
            Assert.NotNull(fixture.Attempt.ActivePromotionId);
            Assert.False(StagingPromotionResendPolicy.IsDue(fixture.Attempt.PromotionMessageSentDate));
            Assert.Equal(fixture.Attempt.ActivePromotionId, message.PromotionId);
            Assert.Equal(StagingPromotionTargetType.StagedSymbolPackage, message.TargetType);
            Assert.Equal(fixture.Attempt.Key, message.TargetKey);
            Assert.Equal(PackageStatus.Available, fixture.Attempt.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.Equal(PackageStatus.Staged, fixture.Attempt.SymbolPackage.StatusKey);
        }

        [Theory]
        [InlineData("validating", PackageStagingPromotionResult.NotReady)]
        [InlineData("validation-failed", PackageStagingPromotionResult.NotReady)]
        [InlineData("private-parent", PackageStagingPromotionResult.NotReady)]
        [InlineData("deleted-parent", PackageStagingPromotionResult.NotReady)]
        [InlineData("stale-attempt", PackageStagingPromotionResult.NotReady)]
        [InlineData("published-attempt", PackageStagingPromotionResult.NotReady)]
        [InlineData("grouped", PackageStagingPromotionResult.Grouped)]
        [InlineData("expired", PackageStagingPromotionResult.NotReady)]
        public async Task KeepsAcceptanceAndApiEligibilityAligned(string blocker, PackageStagingPromotionResult expected)
        {
            var fixture = new Fixture();
            var identity = fixture.Attempt.StagedPackageIdentity;
            switch (blocker)
            {
                case "validating":
                    fixture.Attempt.Status = StagedPackageStatus.Validating;
                    break;
                case "validation-failed":
                    fixture.Attempt.Status = StagedPackageStatus.FailedValidation;
                    break;
                case "private-parent":
                    identity.Package.PackageStatusKey = PackageStatus.Staged;
                    break;
                case "deleted-parent":
                    identity.Package.PackageStatusKey = PackageStatus.Deleted;
                    break;
                case "stale-attempt":
                    identity.CurrentStagedSymbolPackageKey++;
                    break;
                case "published-attempt":
                    fixture.Attempt.SymbolPackage.StatusKey = PackageStatus.Available;
                    break;
                case "grouped":
                    identity.StagingGroup = new StagingGroup { Key = 10 };
                    identity.StagingGroupKey = identity.StagingGroup.Key;
                    break;
                case "expired":
                    fixture.Attempt.ExpirationDate = DateTime.UtcNow;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown test blocker '{blocker}'.");
            }

            var response = StagingArtifactResponse.FromSymbolPackage(fixture.Attempt, DateTime.UtcNow.AddDays(30), "management");
            var result = await fixture.Service.PromoteSymbolPackageAsync(fixture.Owner, fixture.Attempt);

            Assert.Equal(expected, result);
            Assert.False(response.CanPromote);
            Assert.NotEmpty(response.Blockers);
            if (blocker == "expired")
            {
                Assert.Equal("expired", response.Status);
                Assert.Contains(response.Blockers, item => item.Code == "StagingExpired");
                Assert.Equal(StagedPackageStatus.Ready, fixture.Attempt.Status);
            }

            Assert.Null(fixture.Attempt.ActivePromotionId);
            fixture.Repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            fixture.Enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RechecksOwnerAuthorizationBeforeInitialPromotionOrResend(bool resend)
        {
            var fixture = new Fixture();
            fixture.Authorization.Setup(x => x.CanManage(fixture.Owner, fixture.Attempt)).Returns(false);
            fixture.Attempt.Status = resend ? StagedPackageStatus.Promoting : StagedPackageStatus.Ready;
            fixture.Attempt.ActivePromotionId = Guid.NewGuid();
            fixture.Attempt.PromotionMessageSentDate = DateTime.UtcNow.AddHours(-2);

            var result = resend
                ? await fixture.Service.ResendSymbolPackageAsync(fixture.Owner, fixture.Attempt)
                : await fixture.Service.PromoteSymbolPackageAsync(fixture.Owner, fixture.Attempt);

            Assert.Equal(PackageStagingPromotionResult.Unauthorized, result);
            fixture.Repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            fixture.Enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ResendsActivePromotionWithTheSameCorrelation(bool deliveryFailed)
        {
            var fixture = new Fixture();
            var response = StagingArtifactResponse.FromSymbolPackage(fixture.Attempt, DateTime.UtcNow.AddDays(30), "management");
            Assert.True(response.CanPromote);
            Assert.Empty(response.Blockers);
            Assert.Null(response.Group);
            if (deliveryFailed)
            {
                fixture.Enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).ThrowsAsync(new TimeoutException());
            }
            else
            {
                fixture.Enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).Returns(Task.CompletedTask);
            }

            var expected = deliveryFailed ? PackageStagingPromotionResult.DispatchFailed : PackageStagingPromotionResult.Accepted;
            Assert.Equal(expected, await fixture.Service.PromoteSymbolPackageAsync(fixture.Owner, fixture.Attempt));
            if (!deliveryFailed)
            {
                fixture.Attempt.PromotionMessageSentDate = DateTime.UtcNow.AddHours(-2);
            }

            var promotionId = fixture.Attempt.ActivePromotionId;
            var expirationDate = DateTime.UtcNow.AddDays(-1);
            fixture.Attempt.ExpirationDate = expirationDate;
            Assert.Equal(StagedPackageStatus.Promoting, fixture.Attempt.Status);
            Assert.True(StagedSymbolPackagePromotionEligibility.CanResend(fixture.Attempt));
            fixture.Enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).Returns(Task.CompletedTask);
            Assert.Equal(PackageStagingPromotionResult.Accepted, await fixture.Service.ResendSymbolPackageAsync(fixture.Owner, fixture.Attempt));
            Assert.Equal(promotionId, fixture.Attempt.ActivePromotionId);
            Assert.Equal(expirationDate, fixture.Attempt.ExpirationDate);
            Assert.False(StagedSymbolPackagePromotionEligibility.CanResend(fixture.Attempt));
            fixture.Enqueuer.Verify(x => x.SendMessageAsync(It.Is<StagingPromotionMessage>(message =>
                message.PromotionId == promotionId && message.TargetKey == fixture.Attempt.Key && message.TargetType == StagingPromotionTargetType.StagedSymbolPackage)), Times.Exactly(2));
            fixture.Repository.Verify(x => x.CommitChangesAsync(), Times.Exactly(deliveryFailed ? 3 : 2));
        }

        [Fact]
        public async Task PairedSymbolsCannotBeResentUntilTheParentIsPublic()
        {
            var fixture = new Fixture();
            fixture.Attempt.Status = StagedPackageStatus.Promoting;
            fixture.Attempt.ActivePromotionId = Guid.NewGuid();
            fixture.Attempt.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Staged;
            var promotionId = fixture.Attempt.ActivePromotionId;

            Assert.Equal(PackageStagingPromotionResult.NotReady, await fixture.Service.ResendSymbolPackageAsync(fixture.Owner, fixture.Attempt));
            fixture.Repository.Verify(repository => repository.CommitChangesAsync(), Times.Never);
            fixture.Enqueuer.Verify(enqueuer => enqueuer.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
            fixture.Attempt.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;

            Assert.Equal(PackageStagingPromotionResult.Accepted, await fixture.Service.ResendSymbolPackageAsync(fixture.Owner, fixture.Attempt));
            Assert.Equal(promotionId, fixture.Attempt.ActivePromotionId);
        }

        [Fact]
        public async Task FailedPromotionCannotBePromotedOrResent()
        {
            var fixture = new Fixture();
            fixture.Attempt.Status = StagedPackageStatus.PromotionFailed;
            var promotionId = Guid.NewGuid();
            fixture.Attempt.ActivePromotionId = promotionId;
            fixture.Attempt.PromotionMessageSentDate = DateTime.UtcNow.AddHours(-2);
            fixture.Attempt.StagedPackageIdentity.Package.SymbolPackages.Add(new SymbolPackage { StatusKey = PackageStatus.Available });

            var response = StagingArtifactResponse.FromSymbolPackage(fixture.Attempt, DateTime.UtcNow.AddDays(30), "management");
            Assert.False(response.CanPromote);
            Assert.Contains(response.Blockers, blocker => blocker.Code == "SymbolsNotReady");
            Assert.Equal(PackageStagingPromotionResult.NotReady, await fixture.Service.PromoteSymbolPackageAsync(fixture.Owner, fixture.Attempt));
            Assert.Equal(PackageStagingPromotionResult.NotReady, await fixture.Service.ResendSymbolPackageAsync(fixture.Owner, fixture.Attempt));

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(promotionId, fixture.Attempt.ActivePromotionId);
            fixture.Repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            fixture.Enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Fact]
        public async Task ResendsPreexistingActivePromotionWithoutASentDate()
        {
            var fixture = new Fixture();
            fixture.Attempt.Status = StagedPackageStatus.Promoting;
            fixture.Attempt.ActivePromotionId = Guid.NewGuid();
            fixture.Enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).Returns(Task.CompletedTask);
            var promotionId = fixture.Attempt.ActivePromotionId;

            Assert.Equal(PackageStagingPromotionResult.Accepted, await fixture.Service.ResendSymbolPackageAsync(fixture.Owner, fixture.Attempt));

            Assert.Equal(promotionId, fixture.Attempt.ActivePromotionId);
            Assert.NotNull(fixture.Attempt.PromotionMessageSentDate);
            fixture.Enqueuer.Verify(x => x.SendMessageAsync(It.Is<StagingPromotionMessage>(message => message.PromotionId == promotionId)), Times.Once);
        }

        [Theory]
        [InlineData(StagedPackageStatus.Promoting, -59, true)]
        [InlineData(StagedPackageStatus.Promoting, -120, false)]
        public async Task DoesNotResendRecentOrUncorrelatedPromotions(StagedPackageStatus status, int minutesAgo, bool correlated)
        {
            var fixture = new Fixture();
            fixture.Attempt.Status = status;
            fixture.Attempt.ActivePromotionId = correlated ? Guid.NewGuid() : null;
            fixture.Attempt.PromotionMessageSentDate = DateTime.UtcNow.AddMinutes(minutesAgo);

            Assert.Equal(PackageStagingPromotionResult.NotReady, await fixture.Service.ResendSymbolPackageAsync(fixture.Owner, fixture.Attempt));

            fixture.Repository.Verify(x => x.CommitChangesAsync(), Times.Never);
            fixture.Enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReportsInitialOrRecoverySaveConflicts(bool duringRecovery)
        {
            var fixture = new Fixture();
            var commits = 0;
            fixture.Repository.Setup(x => x.CommitChangesAsync()).Returns(() =>
            {
                if (++commits == (duringRecovery ? 2 : 1))
                {
                    throw new DbUpdateConcurrencyException();
                }

                return Task.CompletedTask;
            });
            fixture.Enqueuer.Setup(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>())).ThrowsAsync(new TimeoutException());

            Assert.Equal(PackageStagingPromotionResult.Conflict, await fixture.Service.PromoteSymbolPackageAsync(fixture.Owner, fixture.Attempt));

            fixture.Enqueuer.Verify(x => x.SendMessageAsync(It.IsAny<StagingPromotionMessage>()), duringRecovery ? Times.Once() : Times.Never());
        }

        private class Fixture
        {
            internal User Owner { get; } = new User("owner") { Key = 1 };

            internal StagedSymbolPackage Attempt { get; }

            internal Mock<IPackageStagingAuthorizationService> Authorization { get; } = new Mock<IPackageStagingAuthorizationService>();

            internal Mock<IEntityRepository<StagedSymbolPackage>> Repository { get; } = new Mock<IEntityRepository<StagedSymbolPackage>>();

            internal Mock<IStagingPromotionMessageEnqueuer> Enqueuer { get; } = new Mock<IStagingPromotionMessageEnqueuer>();

            internal PackageStagingPromotionService Service { get; }

            internal Fixture()
            {
                var package = new Package { Key = 2, PackageStatusKey = PackageStatus.Available, PackageRegistration = new PackageRegistration { Id = "PackageA" }, NormalizedVersion = "1.0.0" };
                var identity = new StagedPackageIdentity { Key = package.Key, Package = package, Owner = Owner, OwnerKey = Owner.Key, CurrentStagedSymbolPackageKey = 3 };
                Attempt = new StagedSymbolPackage
                {
                    Key = 3,
                    StagedPackageIdentity = identity,
                    StagedPackageIdentityKey = identity.Key,
                    SymbolPackage = new SymbolPackage { Key = 4, StatusKey = PackageStatus.Staged },
                    SymbolPackageKey = 4,
                    Status = StagedPackageStatus.Ready,
                };
                identity.CurrentStagedSymbolPackage = Attempt;
                Authorization.Setup(x => x.CanManage(Owner, Attempt)).Returns(true);
                Repository.Setup(x => x.CommitChangesAsync()).Returns(Task.CompletedTask);
                var packages = Mock.Of<IEntityRepository<StagedPackage>>(x => x.GetAll() == Array.Empty<StagedPackage>().AsQueryable());
                Service = new PackageStagingPromotionService(Authorization.Object, Enqueuer.Object, packages, Repository.Object);
            }
        }
    }
}

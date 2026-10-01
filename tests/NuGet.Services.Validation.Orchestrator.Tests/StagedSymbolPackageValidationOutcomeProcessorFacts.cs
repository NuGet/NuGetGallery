// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Leases;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StagedSymbolPackageValidationOutcomeProcessorFacts
    {
        [Fact]
        public async Task PublishesBeforePersistingCompletionAndRemovesAttemptLast()
        {
            var fixture = new Fixture();

            await fixture.ProcessAsync();

            Assert.Equal(new[] { "publish", "save-set", "delete-validation-blob", "cleanup" }, fixture.Calls);
            Assert.Equal(ValidationSetStatus.Completed, fixture.Set.ValidationSetStatus);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FailedOrTimedOutIngestionFailsPromotionEvenWhenOrdinaryIngestionIsOptional(bool timedOut)
        {
            var fixture = new Fixture();
            fixture.Ingestion.ValidationStatus = timedOut ? ValidationStatus.Incomplete : ValidationStatus.Failed;
            fixture.Set.Created = DateTime.UtcNow.AddDays(-1);

            await fixture.ProcessAsync();

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(ValidationSetStatus.Completed, fixture.Set.ValidationSetStatus);
            Assert.Equal("fail", fixture.Calls[0]);
            fixture.Storage.Verify(service => service.UpdateValidationStatusAsync(fixture.Ingestion, NuGetValidationResponse.Failed),
                timedOut ? Times.Once() : Times.Never());
            fixture.Promotion.Verify(service => service.CompleteAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PendingIngestionSchedulesOnlyTheRequestedCheck(bool scheduleNextCheck)
        {
            var fixture = new Fixture();
            fixture.Ingestion.ValidationStatus = ValidationStatus.Incomplete;

            await fixture.ProcessAsync(scheduleNextCheck);

            Assert.Equal(new[] { "save-set" }, fixture.Calls);
            Assert.Equal(ValidationSetStatus.InProgress, fixture.Set.ValidationSetStatus);
            fixture.Enqueuer.Verify(service => service.SendMessageAsync(
                It.Is<PackageValidationMessageData>(message => message.ProcessValidationSet.ValidationTrackingId == fixture.Set.ValidationTrackingId
                    && message.ProcessValidationSet.EntityKey == fixture.Attempt.Key
                    && message.ProcessValidationSet.ValidatingType == ValidatingType.StagedSymbolPackage),
                It.IsAny<DateTimeOffset>()), scheduleNextCheck ? Times.Once() : Times.Never());
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public async Task ValidationDatabaseFailureRetainsAttemptForCompletionRetry(bool grouped, bool succeeded)
        {
            var fixture = new Fixture();
            if (grouped)
            {
                fixture.AddToGroup();
            }

            if (!succeeded)
            {
                fixture.Ingestion.ValidationStatus = ValidationStatus.Failed;
            }

            fixture.Storage.Setup(service => service.UpdateValidationSetAsync(fixture.Set))
                .ThrowsAsync(new InvalidOperationException("Validation database unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ProcessAsync());

            Assert.Equal(grouped ? StagedPackageStatus.Promoting : StagedPackageStatus.Succeeded, fixture.Attempt.Status);
            fixture.Promotion.Verify(service => service.CleanUpAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);

            fixture.Set.ValidationSetStatus = ValidationSetStatus.InProgress;
            fixture.Storage.Setup(service => service.UpdateValidationSetAsync(fixture.Set)).Returns(Task.CompletedTask);
            var validators = new Mock<IValidationSetProcessor>(MockBehavior.Strict);
            Assert.True(await fixture.CreateHandler(validators.Object).HandleAsync(fixture.CreateMessage("process")));

            Assert.Equal(ValidationSetStatus.Completed, fixture.Set.ValidationSetStatus);
            Assert.Equal(succeeded ? StagedPackageStatus.Succeeded : StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            fixture.Promotion.Verify(service => service.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId), succeeded ? Times.Once() : Times.Never());
            fixture.Promotion.Verify(service => service.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId), Times.Once);
            validators.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("process")]
        [InlineData("check")]
        [InlineData("fail")]
        public async Task CompletedMessageRetriesCleanupWithoutRunningValidatorsOrPublishingAgain(string messageType)
        {
            var fixture = new Fixture();
            fixture.Files.SetupSequence(service => service.DeletePackageForValidationSetAsync(fixture.Set))
                .ThrowsAsync(new InvalidOperationException("Storage unavailable"))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ProcessAsync());
            Assert.Equal(ValidationSetStatus.Completed, fixture.Set.ValidationSetStatus);
            fixture.Promotion.Verify(service => service.CleanUpAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);

            var validators = new Mock<IValidationSetProcessor>(MockBehavior.Strict);

            Assert.True(await fixture.CreateHandler(validators.Object).HandleAsync(fixture.CreateMessage(messageType)));

            fixture.Promotion.Verify(service => service.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId), Times.Once);
            fixture.Promotion.Verify(service => service.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId), Times.Once);
            validators.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("process")]
        [InlineData("check")]
        [InlineData("fail")]
        public async Task FailedPromotionMessagesFinalizeWithoutRunningValidators(string messageType)
        {
            var fixture = new Fixture();
            fixture.Attempt.Status = StagedPackageStatus.PromotionFailed;
            fixture.Ingestion.ValidationStatus = ValidationStatus.Incomplete;
            var validators = new Mock<IValidationSetProcessor>(MockBehavior.Strict);

            Assert.True(await fixture.CreateHandler(validators.Object).HandleAsync(fixture.CreateMessage(messageType)));

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(ValidationStatus.Incomplete, fixture.Ingestion.ValidationStatus);
            Assert.Equal(ValidationSetStatus.Completed, fixture.Set.ValidationSetStatus);
            Assert.Equal(new[] { "save-set", "delete-validation-blob", "cleanup" }, fixture.Calls);
            validators.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("check")]
        [InlineData("fail")]
        [InlineData("check", true)]
        public async Task StaleCallbackIsDroppedBeforeReadingOrChangingValidatorStatus(string messageType, bool inactiveGroup = false)
        {
            var fixture = new Fixture();
            if (inactiveGroup)
            {
                fixture.AddToGroup();
                fixture.Attempt.StagedPackageIdentity.StagingGroup.ActivePromotionId = Guid.NewGuid();
            }
            else
            {
                fixture.Attempt.ActivePromotionId = Guid.NewGuid();
            }

            var validators = new Mock<IValidationSetProcessor>(MockBehavior.Strict);

            Assert.True(await fixture.CreateHandler(validators.Object).HandleAsync(fixture.CreateMessage(messageType)));

            Assert.Equal(StagedPackageStatus.Promoting, fixture.Attempt.Status);
            Assert.Equal(ValidationSetStatus.InProgress, fixture.Set.ValidationSetStatus);
            Assert.Empty(fixture.Calls);
            validators.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("process")]
        [InlineData("check")]
        public async Task UnavailableLeaseDefersWorkExceptQueueBack(string messageType)
        {
            var fixture = new Fixture();
            fixture.Features.Setup(service => service.IsOrchestratorLeaseEnabled()).Returns(true);
            fixture.Leases.Setup(service => service.TryAcquireAsync("StagedSymbolPackage/packagea/1.0.0", TimeSpan.FromMinutes(1), CancellationToken.None))
                .ReturnsAsync(LeaseResult.Failure());
            var message = fixture.CreateMessage(messageType);
            var validators = new Mock<IValidationSetProcessor>(MockBehavior.Strict);

            Assert.True(await fixture.CreateHandler(validators.Object).HandleAsync(message));

            Assert.Empty(fixture.Calls);
            fixture.Enqueuer.Verify(service => service.SendMessageAsync(message, It.IsAny<DateTimeOffset>()),
                messageType == "check" ? Times.Never() : Times.Once());
            fixture.Leases.Verify(service => service.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            validators.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AcquiredLeaseIsReleasedWhenPublicationThrows()
        {
            var fixture = new Fixture();
            fixture.Features.Setup(service => service.IsOrchestratorLeaseEnabled()).Returns(true);
            fixture.Leases.Setup(service => service.TryAcquireAsync("StagedSymbolPackage/packagea/1.0.0", TimeSpan.FromMinutes(1), CancellationToken.None))
                .ReturnsAsync(LeaseResult.Success("lease"));
            fixture.Leases.Setup(service => service.ReleaseAsync("StagedSymbolPackage/packagea/1.0.0", "lease", CancellationToken.None)).ReturnsAsync(true);
            fixture.Promotion.Setup(service => service.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId))
                .ThrowsAsync(new InvalidOperationException("Publication unavailable"));
            var validators = new Mock<IValidationSetProcessor>();
            validators.Setup(service => service.ProcessValidationsAsync(fixture.Set)).ReturnsAsync(new ValidationSetProcessorResult());
            var handler = fixture.CreateHandler(validators.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(fixture.CreateMessage("process")));

            Assert.Equal(ValidationSetStatus.InProgress, fixture.Set.ValidationSetStatus);
            validators.Verify(service => service.ProcessValidationsAsync(fixture.Set), Times.Once);
            fixture.Leases.Verify(service => service.ReleaseAsync("StagedSymbolPackage/packagea/1.0.0", "lease", CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task ForcedFailureUsesExistingProcessorForAnActivePromotion()
        {
            var fixture = new Fixture();
            fixture.Ingestion.ValidationStatus = ValidationStatus.Incomplete;
            var validators = new Mock<IValidationSetProcessor>(MockBehavior.Strict);
            validators.Setup(service => service.ForceFailValidationSetAsync(fixture.Set))
                .Callback(() => fixture.Ingestion.ValidationStatus = ValidationStatus.Failed)
                .ReturnsAsync(new ValidationSetProcessorResult());

            Assert.True(await fixture.CreateHandler(validators.Object).HandleAsync(fixture.CreateMessage("fail")));

            Assert.Equal(StagedPackageStatus.PromotionFailed, fixture.Attempt.Status);
            Assert.Equal(ValidationSetStatus.Completed, fixture.Set.ValidationSetStatus);
            validators.Verify(service => service.ForceFailValidationSetAsync(fixture.Set), Times.Once);
            validators.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task StaleOutcomeCannotCompleteNewPromotion()
        {
            var fixture = new Fixture();
            fixture.Attempt.ActivePromotionId = Guid.NewGuid();

            await fixture.ProcessAsync();

            Assert.Empty(fixture.Calls);
            Assert.Equal(ValidationSetStatus.InProgress, fixture.Set.ValidationSetStatus);
        }

        private class Fixture
        {
            public Fixture()
            {
                var package = new Package { NormalizedVersion = "1.0.0", PackageRegistration = new PackageRegistration { Id = "PackageA" } };
                Attempt = new StagedSymbolPackage
                {
                    Key = 43,
                    Status = StagedPackageStatus.Promoting,
                    ActivePromotionId = PromotionId,
                    UploadedBlobETag = "etag",
                    StagedPackageIdentity = new StagedPackageIdentity { Package = package, CurrentStagedSymbolPackageKey = 43 },
                };
                Entity = new StagedSymbolPackageValidatingEntity(Attempt);
                Ingestion = new PackageValidation { Key = Guid.NewGuid(), Type = ValidatorName.SymbolsIngester, ValidationStatus = ValidationStatus.Succeeded };
                Set = new PackageValidationSet
                {
                    PackageKey = Attempt.Key,
                    PackageId = "PackageA",
                    PackageNormalizedVersion = "1.0.0",
                    PackageETag = "etag",
                    ValidatingType = ValidatingType.StagedSymbolPackage,
                    ValidationTrackingId = SymbolPromotionValidationTrackingId.Create(PromotionId, Attempt.Key),
                    ValidationSetStatus = ValidationSetStatus.InProgress,
                    Created = DateTime.UtcNow,
                    PackageValidations = new List<PackageValidation> { Ingestion },
                };
                var configuration = new ValidationConfiguration
                {
                    EnableStagedSymbolPromotion = true,
                    MissingPackageRetryCount = 3,
                    TimeoutValidationSetAfter = TimeSpan.FromHours(1),
                    ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(1),
                    Validations = new List<ValidationConfigurationItem>
                    {
                        new ValidationConfigurationItem { Name = ValidatorName.SymbolsIngester, ShouldStart = true, FailureBehavior = ValidationFailureBehavior.AllowedToFail },
                    },
                };
                var options = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
                options.SetupGet(value => value.Value).Returns(configuration);
                Configuration = options.Object;
                Features.Setup(service => service.IsQueueBackEnabled()).Returns(true);
                Promotion.Setup(service => service.CompleteAsync(Attempt.Key, PromotionId)).Callback(() =>
                {
                    Calls.Add("publish");
                    Attempt.Status = StagedPackageStatus.Succeeded;
                }).Returns(Task.CompletedTask);
                Promotion.Setup(service => service.FailAsync(Attempt.Key, PromotionId)).Callback(() =>
                {
                    Calls.Add("fail");
                    Attempt.Status = StagedPackageStatus.PromotionFailed;
                }).Returns(Task.CompletedTask);
                Promotion.Setup(service => service.CleanUpAsync(Attempt.Key, PromotionId)).Callback(() => Calls.Add("cleanup")).Returns(Task.CompletedTask);
                Storage.Setup(service => service.UpdateValidationSetAsync(Set)).Callback(() => Calls.Add("save-set")).Returns(Task.CompletedTask);
                Files.Setup(service => service.DeletePackageForValidationSetAsync(Set)).Callback(() => Calls.Add("delete-validation-blob")).Returns(Task.CompletedTask);
                Target = CreateTarget(new Lazy<IStagedSymbolPackagePromotionService>(() => Promotion.Object));
            }

            public Guid PromotionId { get; } = Guid.NewGuid();

            public StagedSymbolPackage Attempt { get; }

            public IValidatingEntity<StagedSymbolPackage> Entity { get; }

            public PackageValidationSet Set { get; }

            public PackageValidation Ingestion { get; }

            public List<string> Calls { get; } = new List<string>();

            public IOptionsSnapshot<ValidationConfiguration> Configuration { get; }

            public Mock<IStagedSymbolPackagePromotionService> Promotion { get; } = new Mock<IStagedSymbolPackagePromotionService>();

            public Mock<IValidationStorageService> Storage { get; } = new Mock<IValidationStorageService>();

            public Mock<IValidationFileService> Files { get; } = new Mock<IValidationFileService>();

            public Mock<IPackageValidationEnqueuer> Enqueuer { get; } = new Mock<IPackageValidationEnqueuer>();

            public Mock<ILeaseService> Leases { get; } = new Mock<ILeaseService>();

            public Mock<IFeatureFlagService> Features { get; } = new Mock<IFeatureFlagService>();

            public StagedSymbolPackageValidationOutcomeProcessor Target { get; set; }

            public void AddToGroup()
            {
                Attempt.StagedPackageIdentity.StagingGroupKey = 7;
                Attempt.StagedPackageIdentity.StagingGroup = new StagingGroup { Key = 7, ActivePromotionId = PromotionId };
                Attempt.SymbolPackage = new SymbolPackage { StatusKey = PackageStatus.Staged };
                Promotion.Setup(service => service.CompleteAsync(Attempt.Key, PromotionId)).Callback(() =>
                {
                    Calls.Add("publish");
                    Attempt.SymbolPackage.StatusKey = PackageStatus.Available;
                }).Returns(Task.CompletedTask);
                Promotion.Setup(service => service.FailAsync(Attempt.Key, PromotionId))
                    .Callback(() => Calls.Add("fail")).Returns(Task.CompletedTask);
                Promotion.Setup(service => service.CleanUpAsync(Attempt.Key, PromotionId)).Callback(() =>
                {
                    Assert.Equal(ValidationSetStatus.Completed, Set.ValidationSetStatus);
                    Calls.Add("cleanup");
                    if (Attempt.SymbolPackage.StatusKey == PackageStatus.Available)
                    {
                        Attempt.Status = StagedPackageStatus.Succeeded;
                    }
                    else
                    {
                        Attempt.Status = StagedPackageStatus.PromotionFailed;
                    }
                }).Returns(Task.CompletedTask);
            }

            public Task ProcessAsync(bool scheduleNextCheck = true)
            {
                return Target.ProcessValidationOutcomeAsync(Set, Entity, new ValidationSetProcessorResult(), scheduleNextCheck);
            }

            public StagedSymbolPackagePromotionValidationMessageHandler CreateHandler(IValidationSetProcessor validators)
            {
                var entities = new Mock<IEntityService<StagedSymbolPackage>>();
                entities.Setup(service => service.FindPackageByKey(Attempt.Key)).Returns(Entity);
                var provider = new Mock<IValidationSetProvider<StagedSymbolPackage>>();
                provider.Setup(service => service.TryGetOrCreateValidationSetAsync(It.IsAny<ProcessValidationSetData>(), Entity)).ReturnsAsync(Set);
                provider.Setup(service => service.TryGetParentValidationSetAsync(Ingestion.Key)).ReturnsAsync(Set);
                Storage.Setup(service => service.GetValidationSetAsync(Set.ValidationTrackingId)).ReturnsAsync(Set);
                return new StagedSymbolPackagePromotionValidationMessageHandler(Configuration, entities.Object, provider.Object, validators, Target,
                    Storage.Object, Leases.Object, Enqueuer.Object, Features.Object, Mock.Of<ITelemetryService>(),
                    Mock.Of<ILogger<StagedSymbolPackagePromotionValidationMessageHandler>>());
            }

            public PackageValidationMessageData CreateMessage(string type)
            {
                switch (type)
                {
                    case "check":
                        return PackageValidationMessageData.NewCheckValidator(Ingestion.Key);
                    case "fail":
                        return PackageValidationMessageData.NewFailValidationSet(Set.ValidationTrackingId);
                    case "process":
                        return PackageValidationMessageData.NewProcessValidationSet(Set.PackageId, Set.PackageNormalizedVersion,
                            Set.ValidationTrackingId, ValidatingType.StagedSymbolPackage, Attempt.Key);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type));
                }
            }

            public StagedSymbolPackageValidationOutcomeProcessor CreateTarget(Lazy<IStagedSymbolPackagePromotionService> promotion)
            {
                return new StagedSymbolPackageValidationOutcomeProcessor(promotion, Storage.Object, Files.Object, Enqueuer.Object,
                    Configuration, Mock.Of<ITelemetryService>(), Mock.Of<ILogger<StagedSymbolPackageValidationOutcomeProcessor>>());
            }
        }
    }
}

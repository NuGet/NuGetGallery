// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
            fixture.NormalStatus.VerifyNoOtherCalls();
            fixture.Messages.VerifyNoOtherCalls();
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
            fixture.Messages.VerifyNoOtherCalls();
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

        [Fact]
        public async Task ValidationDatabaseFailureRetainsSucceededAttemptForRetry()
        {
            var fixture = new Fixture();
            fixture.Storage.Setup(service => service.UpdateValidationSetAsync(fixture.Set))
                .ThrowsAsync(new InvalidOperationException("Validation database unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ProcessAsync());

            Assert.Equal(StagedPackageStatus.Succeeded, fixture.Attempt.Status);
            fixture.Promotion.Verify(service => service.CleanUpAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);

            fixture.Set.ValidationSetStatus = ValidationSetStatus.InProgress;
            fixture.Storage.Setup(service => service.UpdateValidationSetAsync(fixture.Set)).Returns(Task.CompletedTask);
            await fixture.ProcessAsync();

            Assert.Equal(ValidationSetStatus.Completed, fixture.Set.ValidationSetStatus);
            fixture.Promotion.Verify(service => service.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId), Times.Once);
        }

        [Fact]
        public async Task CompletedMessageRetriesCleanupWithoutRunningValidatorsOrPublishingAgain()
        {
            var fixture = new Fixture();
            fixture.Files.SetupSequence(service => service.DeletePackageForValidationSetAsync(fixture.Set))
                .ThrowsAsync(new InvalidOperationException("Storage unavailable"))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ProcessAsync());
            Assert.Equal(ValidationSetStatus.Completed, fixture.Set.ValidationSetStatus);
            fixture.Promotion.Verify(service => service.CleanUpAsync(It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);

            var entities = new Mock<IEntityService<StagedSymbolPackage>>();
            entities.Setup(service => service.FindPackageByKey(fixture.Attempt.Key)).Returns(fixture.Entity);
            var provider = new Mock<IValidationSetProvider<StagedSymbolPackage>>();
            provider.Setup(service => service.TryGetOrCreateValidationSetAsync(It.IsAny<ProcessValidationSetData>(), fixture.Entity)).ReturnsAsync(fixture.Set);
            var validators = new Mock<IValidationSetProcessor>(MockBehavior.Strict);
            var handler = new StagedSymbolPackageValidationMessageHandler(fixture.Configuration, entities.Object, provider.Object,
                validators.Object, fixture.Target, fixture.Storage.Object, Mock.Of<ILeaseService>(), fixture.Enqueuer.Object,
                Mock.Of<IFeatureFlagService>(), Mock.Of<ITelemetryService>(), Mock.Of<ILogger<StagedSymbolPackageValidationMessageHandler>>());
            var message = PackageValidationMessageData.NewProcessValidationSet(fixture.Set.PackageId, fixture.Set.PackageNormalizedVersion,
                fixture.Set.ValidationTrackingId, ValidatingType.StagedSymbolPackage, fixture.Attempt.Key);

            Assert.True(await handler.HandleAsync(message));

            fixture.Promotion.Verify(service => service.CompleteAsync(fixture.Attempt.Key, fixture.PromotionId), Times.Once);
            fixture.Promotion.Verify(service => service.CleanUpAsync(fixture.Attempt.Key, fixture.PromotionId), Times.Once);
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

        [Fact]
        public async Task OrdinaryStagedValidationUsesExistingOutcomeWithoutResolvingPromotion()
        {
            var fixture = new Fixture();
            fixture.Attempt.Status = StagedPackageStatus.Validating;
            fixture.Ingestion.Type = ValidatorName.SymbolScan;
            fixture.Target = fixture.CreateTarget(new Lazy<IStagedSymbolPackagePromotionService>(
                () => throw new InvalidOperationException("Ordinary validation must not resolve promotion services.")));

            await fixture.ProcessAsync();

            fixture.NormalStatus.Verify(service => service.SetStatusAsync(fixture.Entity, fixture.Set, PackageStatus.Available), Times.Once);
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
                Ingestion = new PackageValidation { Type = ValidatorName.SymbolsIngester, ValidationStatus = ValidationStatus.Succeeded };
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
                        new ValidationConfigurationItem { Name = ValidatorName.SymbolScan, ShouldStart = true, FailureBehavior = ValidationFailureBehavior.MustSucceed },
                    },
                };
                var options = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
                options.SetupGet(value => value.Value).Returns(configuration);
                Configuration = options.Object;
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

            public Mock<IStatusProcessor<StagedSymbolPackage>> NormalStatus { get; } = new Mock<IStatusProcessor<StagedSymbolPackage>>();

            public Mock<IMessageService<StagedSymbolPackage>> Messages { get; } = new Mock<IMessageService<StagedSymbolPackage>>();

            public StagedSymbolPackageValidationOutcomeProcessor Target { get; set; }

            public Task ProcessAsync(bool scheduleNextCheck = true)
            {
                return Target.ProcessValidationOutcomeAsync(Set, Entity, new ValidationSetProcessorResult(), scheduleNextCheck);
            }

            public StagedSymbolPackageValidationOutcomeProcessor CreateTarget(Lazy<IStagedSymbolPackagePromotionService> promotion)
            {
                var normal = new ValidationOutcomeProcessor<StagedSymbolPackage>(Storage.Object, Enqueuer.Object, NormalStatus.Object,
                    Files.Object, Configuration, Messages.Object, Mock.Of<ITelemetryService>(), Mock.Of<ILogger<ValidationOutcomeProcessor<StagedSymbolPackage>>>());
                return new StagedSymbolPackageValidationOutcomeProcessor(normal, promotion, Storage.Object, Files.Object, Enqueuer.Object,
                    Configuration, Mock.Of<ITelemetryService>(), Mock.Of<ILogger<StagedSymbolPackageValidationOutcomeProcessor>>());
            }
        }
    }
}

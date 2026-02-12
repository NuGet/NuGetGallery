// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Leases;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class BaseValidationMessageHandlerFacts
    {
        public class HandleCheckValidatorAsync : CommonFacts
        {
            public HandleCheckValidatorAsync(ITestOutputHelper output) : base(output)
            {
                ValidationId = new Guid("1f25a05c-4bbb-4366-a0bf-d9acef69cca1");
                Message = PackageValidationMessageData.NewCheckValidator(ValidationId);

                ValidationSetProvider
                    .Setup(x => x.TryGetParentValidationSetAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(() => ValidationSet);
            }

            public Guid ValidationId { get; }
        }

        public class HandleProcessValidationSetAsync : CommonFacts
        {
            public HandleProcessValidationSetAsync(ITestOutputHelper output) : base(output)
            {
                Message = PackageValidationMessageData.NewProcessValidationSet(
                    ValidationSet.PackageId,
                    ValidationSet.PackageNormalizedVersion,
                    ValidationSet.ValidationTrackingId,
                    ValidationSet.ValidatingType,
                    ValidationSet.PackageKey);

                ValidationSetProvider
                    .Setup(x => x.TryGetOrCreateValidationSetAsync(
                        It.IsAny<ProcessValidationSetData>(),
                        It.IsAny<IValidatingEntity<TestEntity>>()))
                    .ReturnsAsync(() => ValidationSet);
            }
        }

        public class HandleFailValidationSetAsync : Facts
        {
            public PackageValidationMessageData Message { get; set; }

            public HandleFailValidationSetAsync(ITestOutputHelper output) : base(output)
            {
                ValidationSet.PackageValidations = new System.Collections.Generic.List<PackageValidation>();

                Message = PackageValidationMessageData.NewFailValidationSet(
                    ValidationSet.ValidationTrackingId,
                    ValidationSet.PackageId,
                    ValidationSet.PackageNormalizedVersion);

                ValidationStorageService
                    .Setup(x => x.GetValidationSetAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(() => ValidationSet);
            }

            [Fact]
            public async Task ReturnsFalseWhenValidationSetNotFound()
            {
                ValidationStorageService
                    .Setup(x => x.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                    .ReturnsAsync((PackageValidationSet)null);

                var success = await Target.HandleAsync(Message);

                Assert.False(success);
                ValidationStorageService.Verify(
                    x => x.GetValidationSetAsync(ValidationSet.ValidationTrackingId),
                    Times.Once);
                VerifyAcquire(Times.Never);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Never);
            }

            [Fact]
            public async Task ReturnsFalseWhenValidatingTypeDoesNotMatch()
            {
                ValidationSet.ValidatingType = ValidatingType.SymbolPackage;

                var success = await Target.HandleAsync(Message);

                Assert.False(success);
                ValidationStorageService.Verify(
                    x => x.GetValidationSetAsync(ValidationSet.ValidationTrackingId),
                    Times.Once);
                VerifyAcquire(Times.Never);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Never);
            }

            [Fact]
            public async Task DropsMessageWhenEntityIsHardDeleted()
            {
                EntityService
                    .Setup(x => x.FindPackageByKey(ValidationSet.PackageKey.Value))
                    .Returns((IValidatingEntity<TestEntity>)null);

                var success = await Target.HandleAsync(Message);

                Assert.True(success);
                ValidationStorageService.Verify(
                    x => x.GetValidationSetAsync(ValidationSet.ValidationTrackingId),
                    Times.Once);
                EntityService.Verify(
                    x => x.FindPackageByKey(ValidationSet.PackageKey.Value),
                    Times.Once);
                VerifyAcquire(Times.Never);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Never);
            }

            [Fact]
            public async Task DropsMessageWhenEntityIsSoftDeleted()
            {
                ValidatingEntity
                    .Setup(x => x.Status)
                    .Returns(PackageStatus.Deleted);

                var success = await Target.HandleAsync(Message);

                Assert.True(success);
                ValidationStorageService.Verify(
                    x => x.GetValidationSetAsync(ValidationSet.ValidationTrackingId),
                    Times.Once);
                EntityService.Verify(
                    x => x.FindPackageByKey(ValidationSet.PackageKey.Value),
                    Times.Once);
                VerifyAcquire(Times.Never);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Never);
            }

            [Fact]
            public async Task DropsMessageWhenValidationSetAlreadyCompleted()
            {
                ValidationSet.ValidationSetStatus = ValidationSetStatus.Completed;

                var success = await Target.HandleAsync(Message);

                Assert.True(success);
                ValidationStorageService.Verify(
                    x => x.GetValidationSetAsync(ValidationSet.ValidationTrackingId),
                    Times.Once);
                EntityService.Verify(
                    x => x.FindPackageByKey(ValidationSet.PackageKey.Value),
                    Times.Once);
                VerifyAcquire(Times.Never);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Never);
            }

            [Fact]
            public async Task ReschedulesMessageWhenLeaseUnavailable()
            {
                LeaseResult = LeaseResult.Failure();
                LeaseService
                    .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(LeaseResult);

                var success = await Target.HandleAsync(Message);

                Assert.True(success);
                VerifyAcquire(Times.Once);
                PackageValidationEnqueuer.Verify(
                    x => x.SendMessageAsync(
                        It.Is<PackageValidationMessageData>(m =>
                            m.Type == PackageValidationMessageType.FailValidationSet &&
                            m.FailValidationSet.ValidationTrackingId == ValidationSet.ValidationTrackingId &&
                            m.FailValidationSet.PackageId == ValidationSet.PackageId &&
                            m.FailValidationSet.PackageVersion == ValidationSet.PackageNormalizedVersion),
                        It.IsAny<DateTimeOffset>()),
                    Times.Once);
                ValidationStorageService.Verify(
                    x => x.UpdateValidationStatusAsync(
                        It.IsAny<PackageValidation>(),
                        It.IsAny<NuGetValidationResponse>()),
                    Times.Never);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Never);
                VerifyRelease(Times.Never);
            }

            [Fact]
            public async Task MarksIncompleteValidationsAsFailedAndProcessesOutcome()
            {
                var validation1 = new PackageValidation { ValidationStatus = ValidationStatus.Incomplete, Type = "Validator1" };
                var validation2 = new PackageValidation { ValidationStatus = ValidationStatus.NotStarted, Type = "Validator2" };
                var validation3 = new PackageValidation { ValidationStatus = ValidationStatus.Succeeded, Type = "Validator3" };
                var validation4 = new PackageValidation { ValidationStatus = ValidationStatus.Failed, Type = "Validator4" };

                ValidationSet.PackageValidations = new System.Collections.Generic.List<PackageValidation>
                {
                    validation1,
                    validation2,
                    validation3,
                    validation4
                };

                var success = await Target.HandleAsync(Message);

                Assert.True(success);
                VerifyAcquire(Times.Once);
                ValidationStorageService.Verify(
                    x => x.UpdateValidationStatusAsync(validation1, It.Is<NuGetValidationResponse>(r => r.Status == ValidationStatus.Failed)),
                    Times.Once);
                ValidationStorageService.Verify(
                    x => x.UpdateValidationStatusAsync(validation2, It.Is<NuGetValidationResponse>(r => r.Status == ValidationStatus.Failed)),
                    Times.Once);
                ValidationStorageService.Verify(
                    x => x.UpdateValidationStatusAsync(validation3, It.IsAny<NuGetValidationResponse>()),
                    Times.Never);
                ValidationStorageService.Verify(
                    x => x.UpdateValidationStatusAsync(validation4, It.IsAny<NuGetValidationResponse>()),
                    Times.Never);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        ValidationSet,
                        ValidatingEntity.Object,
                        It.Is<ValidationSetProcessorResult>(r =>
                            !r.AnyValidationSucceeded &&
                            !r.AnyRequiredValidationSucceeded),
                        false),
                    Times.Once);
                VerifyRelease(Times.Once);
            }

            [Fact]
            public async Task ReleasesLeaseOnExceptionDuringFailure()
            {
                var validation1 = new PackageValidation { ValidationStatus = ValidationStatus.Incomplete, Type = "Validator1" };
                ValidationSet.PackageValidations = new System.Collections.Generic.List<PackageValidation> { validation1 };

                var exception = new InvalidOperationException("Failure during update!");
                ValidationStorageService
                    .Setup(x => x.UpdateValidationStatusAsync(It.IsAny<PackageValidation>(), It.IsAny<NuGetValidationResponse>()))
                    .Throws(exception);

                var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.HandleAsync(Message));
                Assert.Same(exception, actual);

                VerifyAcquire(Times.Once);
                VerifyRelease(Times.Once);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Never);
            }

                [Fact]
                public async Task ProcessesOutcomeWithNoValidationsToFail()
                {
                    var validation1 = new PackageValidation { ValidationStatus = ValidationStatus.Succeeded, Type = "Validator1" };
                    var validation2 = new PackageValidation { ValidationStatus = ValidationStatus.Failed, Type = "Validator2" };

                    ValidationSet.PackageValidations = new System.Collections.Generic.List<PackageValidation>
                    {
                        validation1,
                        validation2
                    };

                    var success = await Target.HandleAsync(Message);

                    Assert.True(success);
                    VerifyAcquire(Times.Once);
                    ValidationStorageService.Verify(
                        x => x.UpdateValidationStatusAsync(It.IsAny<PackageValidation>(), It.IsAny<NuGetValidationResponse>()),
                        Times.Never);
                    ValidationOutcomeProcessor.Verify(
                        x => x.ProcessValidationOutcomeAsync(
                            ValidationSet,
                            ValidatingEntity.Object,
                            It.Is<ValidationSetProcessorResult>(r =>
                                !r.AnyValidationSucceeded &&
                                !r.AnyRequiredValidationSucceeded),
                            false),
                        Times.Once);
                    VerifyRelease(Times.Once);
                }

                [Fact]
                public async Task DoesNotAcquireLeaseWhenFeatureFlagIsDisabled()
                {
                    FeatureFlagService.Setup(x => x.IsOrchestratorLeaseEnabled()).Returns(false);

                    var success = await Target.HandleAsync(Message);

                    Assert.True(success);
                    VerifyAcquire(Times.Never);
                    ValidationOutcomeProcessor.Verify(
                        x => x.ProcessValidationOutcomeAsync(
                            ValidationSet,
                            ValidatingEntity.Object,
                            It.Is<ValidationSetProcessorResult>(r =>
                                !r.AnyValidationSucceeded &&
                                !r.AnyRequiredValidationSucceeded),
                            false),
                        Times.Once);
                    VerifyRelease(Times.Never);
                }

                [Fact]
                public async Task ReleasesAcquiredLeaseOnException()
                {
                    var validation1 = new PackageValidation { ValidationStatus = ValidationStatus.Incomplete, Type = "Validator1" };
                    ValidationSet.PackageValidations = new System.Collections.Generic.List<PackageValidation> { validation1 };

                    var exception = new InvalidOperationException("Failure during update!");
                    ValidationStorageService
                        .Setup(x => x.UpdateValidationStatusAsync(It.IsAny<PackageValidation>(), It.IsAny<NuGetValidationResponse>()))
                        .Throws(exception);

                    var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.HandleAsync(Message));
                    Assert.Same(exception, actual);

                    VerifyAcquire(Times.Once);
                    VerifyRelease(Times.Once);
                    ValidationOutcomeProcessor.Verify(
                        x => x.ProcessValidationOutcomeAsync(
                            It.IsAny<PackageValidationSet>(),
                            It.IsAny<IValidatingEntity<TestEntity>>(),
                            It.IsAny<ValidationSetProcessorResult>(),
                            It.IsAny<bool>()),
                        Times.Never);
                }

                [Fact]
                public async Task ReleasesAcquiredLeaseOnSuccess()
                {
                    var success = await Target.HandleAsync(Message);

                    Assert.True(success);
                    VerifyAcquire(Times.Once);
                    ValidationOutcomeProcessor.Verify(
                        x => x.ProcessValidationOutcomeAsync(
                            ValidationSet,
                            ValidatingEntity.Object,
                            It.Is<ValidationSetProcessorResult>(r =>
                                !r.AnyValidationSucceeded &&
                                !r.AnyRequiredValidationSucceeded),
                            false),
                        Times.Once);
                    VerifyRelease(Times.Once);
                }

                [Fact]
                public async Task SwallowsReleaseException()
                {
                    var updateException = new InvalidOperationException("Update fail!");
                    var validation1 = new PackageValidation { ValidationStatus = ValidationStatus.Incomplete, Type = "Validator1" };
                    ValidationSet.PackageValidations = new System.Collections.Generic.List<PackageValidation> { validation1 };

                    ValidationStorageService
                        .Setup(x => x.UpdateValidationStatusAsync(It.IsAny<PackageValidation>(), It.IsAny<NuGetValidationResponse>()))
                        .Throws(updateException);
                    var leaseException = new ArgumentException("Release fail!");
                    LeaseService
                        .Setup(x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .Throws(leaseException);

                    var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.HandleAsync(Message));
                    Assert.Same(updateException, actual);

                    VerifyAcquire(Times.Once);
                    ValidationOutcomeProcessor.Verify(
                        x => x.ProcessValidationOutcomeAsync(
                            It.IsAny<PackageValidationSet>(),
                            It.IsAny<IValidatingEntity<TestEntity>>(),
                            It.IsAny<ValidationSetProcessorResult>(),
                            It.IsAny<bool>()),
                        Times.Never);
                    VerifyRelease(Times.Once);
                }
            }

        public abstract class CommonFacts : Facts
        {
            public CommonFacts(ITestOutputHelper output) : base(output)
            {
            }

            public PackageValidationMessageData Message { get; set; }

            [Fact]
            public async Task DoesNotAcquireLeaseWhenFeatureFlagIsDisabled()
            {
                FeatureFlagService.Setup(x => x.IsOrchestratorLeaseEnabled()).Returns(false);

                var success = await Target.HandleAsync(Message);

                Assert.True(success);
                VerifyAcquire(Times.Never);
                ValidationSetProcessor.Verify(
                    x => x.ProcessValidationsAsync(It.IsAny<PackageValidationSet>()),
                    Times.Once);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Once);
                VerifyRelease(Times.Never);
            }

            [Fact]
            public async Task ReleasesAcquiredLeaseOnException()
            {
                var exception = new InvalidOperationException("Fail!");
                ValidationSetProcessor
                    .Setup(x => x.ProcessValidationsAsync(It.IsAny<PackageValidationSet>()))
                    .Throws(exception);

                var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.HandleAsync(Message));
                Assert.Same(exception, actual);

                VerifyAcquire(Times.Once);
                ValidationSetProcessor.Verify(
                    x => x.ProcessValidationsAsync(It.IsAny<PackageValidationSet>()),
                    Times.Once);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Never);
                VerifyRelease(Times.Once);
            }

            [Fact]
            public async Task ReleasesAcquiredLeaseOnSuccess()
            {
                var success = await Target.HandleAsync(Message);

                Assert.True(success);
                VerifyAcquire(Times.Once);
                ValidationSetProcessor.Verify(
                    x => x.ProcessValidationsAsync(It.IsAny<PackageValidationSet>()),
                    Times.Once);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Once);
                VerifyRelease(Times.Once);
            }

            [Fact]
            public async Task SwallowsReleaseException()
            {
                var processorException = new InvalidOperationException("Fail!");
                ValidationSetProcessor
                    .Setup(x => x.ProcessValidationsAsync(It.IsAny<PackageValidationSet>()))
                    .Throws(processorException);
                var leaseException = new ArgumentException("Release fail!");
                LeaseService
                    .Setup(x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Throws(leaseException);

                var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.HandleAsync(Message));
                Assert.Same(processorException, actual);

                VerifyAcquire(Times.Once);
                ValidationSetProcessor.Verify(
                    x => x.ProcessValidationsAsync(It.IsAny<PackageValidationSet>()),
                    Times.Once);
                ValidationOutcomeProcessor.Verify(
                    x => x.ProcessValidationOutcomeAsync(
                        It.IsAny<PackageValidationSet>(),
                        It.IsAny<IValidatingEntity<TestEntity>>(),
                        It.IsAny<ValidationSetProcessorResult>(),
                        It.IsAny<bool>()),
                    Times.Never);
                VerifyRelease(Times.Once);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                Options = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
                EntityService = new Mock<IEntityService<TestEntity>>();
                ValidationSetProvider = new Mock<IValidationSetProvider<TestEntity>>();
                ValidationSetProcessor = new Mock<IValidationSetProcessor>();
                ValidationOutcomeProcessor = new Mock<IValidationOutcomeProcessor<TestEntity>>();
                ValidationStorageService = new Mock<IValidationStorageService>();
                LeaseService = new Mock<ILeaseService>();
                PackageValidationEnqueuer = new Mock<IPackageValidationEnqueuer>();
                FeatureFlagService = new Mock<IFeatureFlagService>();
                TelemetryService = new Mock<ITelemetryService>();
                Logger = new LoggerFactory().AddXunit(output).CreateLogger<SymbolValidationMessageHandler>();

                Config = new ValidationConfiguration
                {
                    MissingPackageRetryCount = 1,
                };
                ValidatingEntity = new Mock<IValidatingEntity<TestEntity>>();
                ValidationSet = new PackageValidationSet
                {
                    PackageKey = 42,
                    ValidationTrackingId = new Guid("dc2aa638-a23c-4791-a4ff-c3e07b1320a4"),
                    PackageId = "NuGet.Versioning",
                    PackageNormalizedVersion = "5.3.0-BETA",
                    ValidatingType = ValidatingType.Package,
                    ValidationSetStatus = ValidationSetStatus.InProgress,
                };
                LeaseResourceName = "Package/nuget.versioning/5.3.0-beta";
                ValidationSetProcessorResult = new ValidationSetProcessorResult();
                LeaseResult = LeaseResult.Success("lease-id");
                
                Options.Setup(x => x.Value).Returns(() => Config);
                EntityService
                    .Setup(x => x.FindPackageByKey(It.IsAny<int>()))
                    .Returns(() => ValidatingEntity.Object);
                ValidationSetProcessor
                    .Setup(x => x.ProcessValidationsAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync(() => ValidationSetProcessorResult);
                ValidatingEntity
                    .Setup(x => x.Status)
                    .Returns(PackageStatus.Validating);
                LeaseService
                    .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => LeaseResult);

                FeatureFlagService.Setup(x => x.IsQueueBackEnabled()).Returns(true);
                FeatureFlagService.Setup(x => x.IsOrchestratorLeaseEnabled()).Returns(true);

                Target = new TestHandler(
                    Options.Object,
                    EntityService.Object,
                    ValidationSetProvider.Object,
                    ValidationSetProcessor.Object,
                    ValidationOutcomeProcessor.Object,
                    ValidationStorageService.Object,
                    LeaseService.Object,
                    PackageValidationEnqueuer.Object,
                    FeatureFlagService.Object,
                    TelemetryService.Object,
                    Logger);
            }

            public Mock<IOptionsSnapshot<ValidationConfiguration>> Options { get; }
            public Mock<IEntityService<TestEntity>> EntityService { get; }
            public Mock<IValidationSetProvider<TestEntity>> ValidationSetProvider { get; }
            public Mock<IValidationSetProcessor> ValidationSetProcessor { get; }
            public Mock<IValidationOutcomeProcessor<TestEntity>> ValidationOutcomeProcessor { get; }
            public Mock<IValidationStorageService> ValidationStorageService { get; }
            public Mock<ILeaseService> LeaseService { get; }
            public Mock<IPackageValidationEnqueuer> PackageValidationEnqueuer { get; }
            public Mock<IFeatureFlagService> FeatureFlagService { get; }
            public Mock<ITelemetryService> TelemetryService { get; }
            public ILogger<SymbolValidationMessageHandler> Logger { get; }
            public ValidationConfiguration Config { get; }
            public TestHandler Target { get; }
            public Mock<IValidatingEntity<TestEntity>> ValidatingEntity { get; }
            public PackageValidationSet ValidationSet { get; }
            public string LeaseResourceName { get; }
            public ValidationSetProcessorResult ValidationSetProcessorResult { get; }
            public LeaseResult LeaseResult { get; set; }

            public void VerifyAcquire(Func<Times> times)
            {
                LeaseService.Verify(
                    x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                    times);
                LeaseService.Verify(
                    x => x.TryAcquireAsync(LeaseResourceName, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                    times);
            }

            public void VerifyRelease(Func<Times> times)
            {
                LeaseService.Verify(
                    x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                    times);
                LeaseService.Verify(
                    x => x.ReleaseAsync(LeaseResourceName, LeaseResult.LeaseId, It.IsAny<CancellationToken>()),
                    times);
            }
        }

        public class TestHandler : BaseValidationMessageHandler<TestEntity>
        {
            public TestHandler(
                IOptionsSnapshot<ValidationConfiguration> validationConfigsAccessor,
                IEntityService<TestEntity> entityService,
                IValidationSetProvider<TestEntity> validationSetProvider,
                IValidationSetProcessor validationSetProcessor,
                IValidationOutcomeProcessor<TestEntity> validationOutcomeProcessor,
                IValidationStorageService validationStorageService,
                ILeaseService leaseService,
                IPackageValidationEnqueuer validationEnqueuer,
                IFeatureFlagService featureFlagService,
                ITelemetryService telemetryService,
                ILogger logger) : base(
                    validationConfigsAccessor,
                    entityService,
                    validationSetProvider,
                    validationSetProcessor,
                    validationOutcomeProcessor,
                    validationStorageService,
                    leaseService,
                    validationEnqueuer,
                    featureFlagService,
                    telemetryService,
                    logger)
            {
            }

            protected override ValidatingType ValidatingType => ValidatingType.Package;
        }

        public class TestEntity : IEntity
        {
            public int Key { get; set; }
        }
    }
}

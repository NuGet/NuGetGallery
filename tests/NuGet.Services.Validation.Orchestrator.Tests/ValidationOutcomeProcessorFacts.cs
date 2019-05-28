// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationOutcomeProcessorFacts
    {
        [Theory]
        [InlineData(ValidationFailureBehavior.MustSucceed, PackageStatus.FailedValidation)]
        [InlineData(ValidationFailureBehavior.AllowedToFail, PackageStatus.Available)]
        public async Task ProcessesFailedValidationAccordingToFailureBehavior(ValidationFailureBehavior failureBehavior, PackageStatus expectedPackageStatus)
        {
            AddValidation("validation1", ValidationStatus.Failed, failureBehavior);
           
            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            PackageStateProcessorMock.Verify(
                x => x.SetStatusAsync(PackageValidatingEntity, ValidationSet, expectedPackageStatus),
                Times.Once);
            PackageFileServiceMock.Verify(
                x => x.DeletePackageForValidationSetAsync(ValidationSet),
                Times.Once);

            Assert.Equal(ValidationSetStatus.Completed, ValidationSet.ValidationSetStatus);
        }

        [Theory]
        [InlineData(new ValidationIssueCode[0])]
        [InlineData(new[] { ValidationIssueCode.Unknown })]
        [InlineData(new[] { ValidationIssueCode.PackageIsSigned, ValidationIssueCode.Unknown })]
        [InlineData(new[] { ValidationIssueCode.Unknown, ValidationIssueCode.Unknown })]
        [InlineData(new[] { ValidationIssueCode.Unknown, ValidationIssueCode.PackageIsSigned })]
        [InlineData(new[] { ValidationIssueCode.PackageIsSigned })]
        [InlineData(new[] { ValidationIssueCode.PackageIsSigned, ValidationIssueCode.PackageIsSigned })]
        public async Task SendsFailureEmailOnFailedValidation(ValidationIssueCode[] issueCodes)
        {
            AddValidation("validation1", ValidationStatus.Failed);
            ValidationSet.PackageValidations.First().PackageValidationIssues = issueCodes
                .Select(ic => new PackageValidationIssue { IssueCode = ic })
                .ToList();

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            MessageServiceMock
                .Verify(ms => ms.SendValidationFailedMessageAsync(Package, ValidationSet), Times.Once());
            MessageServiceMock
                .Verify(ms => ms.SendValidationFailedMessageAsync(It.IsAny<Package>(), It.IsAny<PackageValidationSet>()), Times.Once());
        }

        [Fact]
        public async Task TracksTimedOutValidators()
        {
            const int postponeMinutes = 1;

            AddValidation(
                "IncompleteAndNotTimedOut",
                ValidationStatus.Incomplete,
                validationStart: DateTime.UtcNow,
                trackAfter: TimeSpan.FromDays(1));
            AddValidation(
                "IncompleteButTimedOut",
                ValidationStatus.Incomplete,
                validationStart: DateTime.UtcNow + TimeSpan.FromDays(-5),
                trackAfter: TimeSpan.FromDays(1));

            Configuration.TimeoutValidationSetAfter = TimeSpan.FromDays(1);
            Configuration.ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(postponeMinutes);

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            TelemetryServiceMock
                .Verify(t => t.TrackValidatorTimeout("IncompleteButTimedOut"));
            ValidationEnqueuerMock
                .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Once);
            PackageFileServiceMock
                .Verify(x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
        }

        [Fact]
        public async Task DoesNotReEnqueueProcessingIfValidationSetTimesOut()
        {
            const int postponeMinutes = 1;

            AddValidation("validation1", ValidationStatus.Incomplete);

            Configuration.TimeoutValidationSetAfter = TimeSpan.FromDays(1);
            Configuration.ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(postponeMinutes);

            ValidationSet.Created = DateTime.UtcNow - TimeSpan.FromDays(1) - TimeSpan.FromHours(1);

            ValidationStorageServiceMock
                .Setup(s => s.UpdateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Callback<PackageValidationSet>(s => s.Updated = DateTime.UtcNow)
                .Returns(Task.FromResult(0));

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            TelemetryServiceMock
                .Verify(t => t.TrackValidationSetTimeout(Package.PackageRegistration.Id, Package.NormalizedVersion, ValidationSet.ValidationTrackingId));
            ValidationEnqueuerMock
                .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Never);
            PackageFileServiceMock
                .Verify(x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);

            Assert.Equal(ValidationSetStatus.InProgress, ValidationSet.ValidationSetStatus);
        }

        [Theory]
        [InlineData(PackageStatus.Available, false)]
        [InlineData(PackageStatus.Deleted, false)]
        [InlineData(PackageStatus.Validating, true)]
        [InlineData(PackageStatus.FailedValidation, false)]
        public async Task SendsValidatingTooLongMessageOnlyIfPackageIsInValidatingState(PackageStatus packageStatus, bool shouldSend)
        {
            const int postponeMinutes = 1;

            AddValidation("validation1", ValidationStatus.Incomplete);

            Configuration.TimeoutValidationSetAfter = TimeSpan.FromDays(1);
            Configuration.ValidationSetNotificationTimeout = TimeSpan.FromMinutes(20);
            Configuration.ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(postponeMinutes);

            ValidationSet.Created = DateTime.UtcNow - TimeSpan.FromMinutes(21);
            ValidationSet.Updated = DateTime.UtcNow - TimeSpan.FromMinutes(15);

            PackageValidatingEntity.EntityRecord.PackageStatusKey = packageStatus;

            ValidationStorageServiceMock
                .Setup(s => s.UpdateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Callback<PackageValidationSet>(s => s.Updated = DateTime.UtcNow)
                .Returns(Task.FromResult(0));

            ValidationStorageServiceMock
                .Setup(s => s.GetValidationSetCountAsync(PackageValidatingEntity))
                .Returns(Task.FromResult(1));

            // Process the outcome once - the "too long to validate" message should be sent.
            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            if (shouldSend)
            {
                TelemetryServiceMock
                    .Verify(t => t.TrackSentValidationTakingTooLongMessage(Package.PackageRegistration.Id, Package.NormalizedVersion, ValidationSet.ValidationTrackingId), Times.Once);
                MessageServiceMock
                    .Verify(m => m.SendValidationTakingTooLongMessageAsync(Package), Times.Once);
                ValidationEnqueuerMock
                    .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Once);
                PackageFileServiceMock
                    .Verify(x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
            }
            else
            {
                TelemetryServiceMock
                    .Verify(t => t.TrackSentValidationTakingTooLongMessage(Package.PackageRegistration.Id, Package.NormalizedVersion, ValidationSet.ValidationTrackingId), Times.Never);
                MessageServiceMock
                    .Verify(m => m.SendValidationTakingTooLongMessageAsync(Package), Times.Never);
                ValidationEnqueuerMock
                    .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Once);
                PackageFileServiceMock
                    .Verify(x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
            }

            TelemetryServiceMock.ResetCalls();
            MessageServiceMock.ResetCalls();
            ValidationEnqueuerMock.ResetCalls();

            // Process the outcome again - the "too long to validate" message should NOT be sent.
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            TelemetryServiceMock
                .Verify(t => t.TrackSentValidationTakingTooLongMessage(Package.PackageRegistration.Id, Package.NormalizedVersion, ValidationSet.ValidationTrackingId), Times.Never);
            MessageServiceMock
                .Verify(m => m.SendValidationTakingTooLongMessageAsync(Package), Times.Never);
            ValidationEnqueuerMock
                .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Once);
            PackageFileServiceMock
                .Verify(x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
        }

        [Fact]
        public async Task DoesNotSendValidatingTooLongMessageOnRevalidations()
        {
            const int postponeMinutes = 1;

            AddValidation("validation1", ValidationStatus.Incomplete);

            Configuration.TimeoutValidationSetAfter = TimeSpan.FromDays(1);
            Configuration.ValidationSetNotificationTimeout = TimeSpan.FromMinutes(20);
            Configuration.ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(postponeMinutes);

            ValidationSet.Created = DateTime.UtcNow - TimeSpan.FromMinutes(21);
            ValidationSet.Updated = DateTime.UtcNow - TimeSpan.FromMinutes(15);

            ValidationStorageServiceMock
                .Setup(s => s.UpdateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Callback<PackageValidationSet>(s => s.Updated = DateTime.UtcNow)
                .Returns(Task.FromResult(0));

            ValidationStorageServiceMock
                .Setup(s => s.GetValidationSetCountAsync(PackageValidatingEntity))
                .Returns(Task.FromResult(2));

            // Process the outcome once - the "too long to validate" message should NOT be sent.
            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            TelemetryServiceMock
                .Verify(t => t.TrackSentValidationTakingTooLongMessage(Package.PackageRegistration.Id, Package.NormalizedVersion, ValidationSet.ValidationTrackingId), Times.Never);
            MessageServiceMock
                .Verify(m => m.SendValidationTakingTooLongMessageAsync(Package), Times.Never);
            ValidationEnqueuerMock
                .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Once);
            PackageFileServiceMock
                .Verify(x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
        }

        [Fact]
        public async Task ReEnqueuesProcessingIfNotAllComplete()
        {
            const int postponeMinutes = 1;
            AddValidation("validation1", ValidationStatus.Incomplete);
            Configuration.ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(postponeMinutes);
            Configuration.TimeoutValidationSetAfter = TimeSpan.FromDays(1);

            PackageValidationMessageData messageData = null;
            DateTimeOffset postponeTill = DateTimeOffset.MinValue;
            ValidationEnqueuerMock
                .Setup(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()))
                .Returns(Task.FromResult(0))
                .Callback<PackageValidationMessageData, DateTimeOffset>((pv, pt) => {
                    messageData = pv;
                    postponeTill = pt;
                });

            var processor = CreateProcessor();
            var startTime = DateTimeOffset.Now;
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            ValidationStorageServiceMock
                .Verify(s => s.UpdateValidationSetAsync(ValidationSet), Times.Once);

            ValidationEnqueuerMock
                .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Once());

            PackageStateProcessorMock.Verify(
                x => x.SetStatusAsync(It.IsAny<PackageValidatingEntity>(), It.IsAny<PackageValidationSet>(), It.IsAny<PackageStatus>()),
                Times.Never);

            PackageFileServiceMock.Verify(
                x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);

            Assert.NotNull(messageData);
            Assert.Equal(ValidationSet.ValidationTrackingId, messageData.ValidationTrackingId);
            Assert.Equal(ValidationSet.PackageId, messageData.PackageId);
            Assert.Equal(Package.NormalizedVersion, messageData.PackageVersion);
            Assert.Equal(ValidationSet.ValidatingType, messageData.ValidatingType);
            Assert.Equal(ValidationSet.PackageKey, messageData.EntityKey);
            Assert.Equal(postponeMinutes, (postponeTill - startTime).TotalMinutes, 0);
            Assert.Equal(ValidationSetStatus.InProgress, ValidationSet.ValidationSetStatus);
        }

        [Fact]
        public async Task DoesNotSendSuccessEmailIfPackageIsAlreadyAvailable()
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            Package.PackageStatusKey = PackageStatus.Available;

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);
           
            MessageServiceMock.Verify(
                x => x.SendPublishedMessageAsync(It.IsAny<Package>()),
                Times.Never);
        }

        [Fact]
        public async Task MakesPackageAvailableAndSendsEmailUponSuccess()
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            Package.PackageStatusKey = PackageStatus.Validating;

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            PackageStateProcessorMock.Verify(
                x => x.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available),
                Times.Once);

            PackageFileServiceMock.Verify(
                x => x.DeletePackageForValidationSetAsync(ValidationSet),
                Times.Once);

            MessageServiceMock
                .Verify(ms => ms.SendPublishedMessageAsync(Package), Times.Once());
            MessageServiceMock
                .Verify(ms => ms.SendPublishedMessageAsync(It.IsAny<Package>()), Times.Once());

            Assert.Equal(ValidationSetStatus.Completed, ValidationSet.ValidationSetStatus);
        }
        
        [Theory]
        [InlineData(PackageStatus.Validating)]
        [InlineData(PackageStatus.FailedValidation)]
        public async Task HasProperOperationOrderWhenTransitioningToAvailable(PackageStatus packageStatus)
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            ProcessorStats.AnyRequiredValidationSucceeded = true;
            Package.PackageStatusKey = packageStatus;

            var operations = RecordOperationOrder();

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            Assert.Equal(
                new[]
                {
                    nameof(IStatusProcessor<Package>.SetStatusAsync),
                    nameof(IValidationStorageService.UpdateValidationSetAsync),
                    nameof(IMessageService<Package>.SendPublishedMessageAsync),
                    nameof(ITelemetryService.TrackTotalValidationDuration),
                    nameof(IValidationFileService.DeletePackageForValidationSetAsync),
                },
                operations.ToArray());
        }

        [Fact]
        public async Task HasProperOperationOrderWhenAlreadyAvailable()
        {
            AddValidation("validation1", ValidationStatus.Succeeded);
            ProcessorStats.AnyRequiredValidationSucceeded = true;
            Package.PackageStatusKey = PackageStatus.Available;

            var operations = RecordOperationOrder();

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            Assert.Equal(
                new[]
                {
                    nameof(IStatusProcessor<Package>.SetStatusAsync),
                    nameof(IValidationStorageService.UpdateValidationSetAsync),
                    nameof(ITelemetryService.TrackTotalValidationDuration),
                    nameof(IValidationFileService.DeletePackageForValidationSetAsync),
                },
                operations.ToArray());
        }

        [Fact]
        public async Task HasProperOperationOrderWhenTransitioningToFailedValidation()
        {
            AddValidation("validation1", ValidationStatus.Failed);
            Package.PackageStatusKey = PackageStatus.Validating;

            var operations = RecordOperationOrder();

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            Assert.Equal(
                new[]
                {
                    nameof(IStatusProcessor<Package>.SetStatusAsync),
                    nameof(IValidationStorageService.UpdateValidationSetAsync),
                    nameof(IMessageService<Package>.SendValidationFailedMessageAsync),
                    nameof(ITelemetryService.TrackTotalValidationDuration),
                    nameof(IValidationFileService.DeletePackageForValidationSetAsync),
                },
                operations.ToArray());
        }

        [Theory]
        [InlineData(PackageStatus.Available, ValidationStatus.Failed)]
        [InlineData(PackageStatus.FailedValidation, ValidationStatus.Failed)]
        public async Task HasProperOperationOrderWhenTerminalAndValidationFailed(PackageStatus packageStatus, ValidationStatus validationStatus)
        {
            AddValidation("validation1", validationStatus);
            Package.PackageStatusKey = packageStatus;

            var operations = RecordOperationOrder();

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            Assert.Equal(
                new[]
                {
                    nameof(IValidationStorageService.UpdateValidationSetAsync),
                    nameof(ITelemetryService.TrackTotalValidationDuration),
                    nameof(IValidationFileService.DeletePackageForValidationSetAsync),
                },
                operations.ToArray());
        }

        [Theory]
        [InlineData(ValidationStatus.Failed, PackageStatus.Validating, PackageStatus.FailedValidation, true)]
        [InlineData(ValidationStatus.Failed, PackageStatus.Available, PackageStatus.Available, false)]
        [InlineData(ValidationStatus.Failed, PackageStatus.FailedValidation, PackageStatus.FailedValidation, false)]
        [InlineData(ValidationStatus.Succeeded, PackageStatus.Validating, PackageStatus.Available, true)]
        [InlineData(ValidationStatus.Succeeded, PackageStatus.Available, PackageStatus.Available, true)]
        [InlineData(ValidationStatus.Succeeded, PackageStatus.FailedValidation, PackageStatus.Available, true)]
        public async Task MarksPackageStatusBasedOnValidatorResults(
            ValidationStatus validation,
            PackageStatus fromStatus,
            PackageStatus toStatus,
            bool expectedSetPackageStatusCall)
        {
            AddValidation("validation1", validation);
            Package.PackageStatusKey = fromStatus;

            TimeSpan duration = default(TimeSpan);
            TelemetryServiceMock
                .Setup(ts => ts.TrackTotalValidationDuration(It.IsAny<TimeSpan>(), It.IsAny<bool>()))
                .Callback<TimeSpan, bool>((t, _) => duration = t);

            ProcessorStats.AnyRequiredValidationSucceeded = true;
            ProcessorStats.AnyValidationSucceeded = true;

            var processor = CreateProcessor();

            var before = DateTime.UtcNow;
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);
            var after = DateTime.UtcNow;

            if (expectedSetPackageStatusCall)
            {
                PackageStateProcessorMock.Verify(
                    x => x.SetStatusAsync(PackageValidatingEntity, ValidationSet, toStatus),
                    Times.Once);
                PackageStateProcessorMock.Verify(
                    x => x.SetStatusAsync(It.IsAny<PackageValidatingEntity>(), It.IsAny<PackageValidationSet>(), It.IsAny<PackageStatus>()),
                    Times.Once);
            }
            else
            {
                PackageStateProcessorMock.Verify(
                    x => x.SetStatusAsync(It.IsAny<PackageValidatingEntity>(), It.IsAny<PackageValidationSet>(), It.IsAny<PackageStatus>()),
                    Times.Never);
            }

            PackageFileServiceMock.Verify(
                x => x.DeletePackageForValidationSetAsync(ValidationSet),
                Times.Once);
            TelemetryServiceMock
                .Verify(ts => ts.TrackTotalValidationDuration(It.IsAny<TimeSpan>(), It.IsAny<bool>()), Times.Once());
            Assert.InRange(duration, before - ValidationSet.Created, after - ValidationSet.Created);

            Assert.Equal(ValidationSetStatus.Completed, ValidationSet.ValidationSetStatus);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task TracksSuccessOnAllRequiredValidatorsFinished(bool requiredValidationSucceeded, bool expectedCompletionTracking)
        {
            AddValidation("requiredValidation", ValidationStatus.Succeeded, ValidationFailureBehavior.MustSucceed);
            AddValidation("optionalValidaiton", ValidationStatus.Incomplete, ValidationFailureBehavior.AllowedToFail);
            ProcessorStats.AnyRequiredValidationSucceeded = requiredValidationSucceeded;

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            if (expectedCompletionTracking)
            {
                TelemetryServiceMock
                    .Verify(ts => ts.TrackTotalValidationDuration(It.IsAny<TimeSpan>(), true), Times.Once());
                TelemetryServiceMock
                    .Verify(ts => ts.TrackTotalValidationDuration(It.IsAny<TimeSpan>(), It.IsAny<bool>()), Times.Once());
            }
            else
            {
                TelemetryServiceMock
                    .Verify(ts => ts.TrackTotalValidationDuration(It.IsAny<TimeSpan>(), It.IsAny<bool>()), Times.Never());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ContinuesCheckingStatusIfOptionalValidationsAreRunning(bool requiredValidationSucceeded)
        {
            AddValidation("requiredValidation", ValidationStatus.Succeeded, ValidationFailureBehavior.MustSucceed);
            AddValidation("optionalValidaiton", ValidationStatus.Incomplete, ValidationFailureBehavior.AllowedToFail);
            ProcessorStats.AnyRequiredValidationSucceeded = requiredValidationSucceeded;
            Configuration.TimeoutValidationSetAfter = TimeSpan.FromDays(1);

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            ValidationEnqueuerMock
                .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Once());

            Assert.Equal(ValidationSetStatus.InProgress, ValidationSet.ValidationSetStatus);
        }

        [Theory]
        [InlineData(true, ValidationStatus.Succeeded)]
        [InlineData(true, ValidationStatus.Failed)]
        [InlineData(false, ValidationStatus.Succeeded)]
        [InlineData(false, ValidationStatus.Failed)]
        public async Task StopsCheckingStatusWhenOptionalValidationsFinish(bool requiredValidationSucceeded, ValidationStatus finalState)
        {
            AddValidation("requiredValidation", ValidationStatus.Succeeded, ValidationFailureBehavior.MustSucceed);
            AddValidation("optionalValidaiton", finalState, ValidationFailureBehavior.AllowedToFail);
            ProcessorStats.AnyRequiredValidationSucceeded = requiredValidationSucceeded;
            Configuration.TimeoutValidationSetAfter = TimeSpan.FromDays(1);

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            ValidationEnqueuerMock
                .Verify(ve => ve.StartValidationAsync(It.IsAny<PackageValidationMessageData>(), It.IsAny<DateTimeOffset>()), Times.Never());

            Assert.Equal(ValidationSetStatus.Completed, ValidationSet.ValidationSetStatus);
        }

        public static IEnumerable<object[]> TwoValidationStatusAndBoolCombinations =>
            from s1 in (ValidationStatus[])Enum.GetValues(typeof(ValidationStatus))
            from s2 in (ValidationStatus[])Enum.GetValues(typeof(ValidationStatus))
            from b1 in new[] { false, true }
            select new object[] { s1, s2, b1 };

        [Theory]
        [MemberData(nameof(TwoValidationStatusAndBoolCombinations))]
        public async Task SendsTooLongNotificationOnlyWhenItConcernsRequiredValidation(
            ValidationStatus requiredValidationState,
            ValidationStatus optionalValidationState,
            bool requiredValidationSucceeded)
        {
            bool expectedNotification = requiredValidationState == ValidationStatus.Incomplete || requiredValidationState == ValidationStatus.NotStarted;

            AddValidation("requiredValidation", requiredValidationState, ValidationFailureBehavior.MustSucceed);
            AddValidation("optionalValidaiton", optionalValidationState, ValidationFailureBehavior.AllowedToFail);
            ProcessorStats.AnyRequiredValidationSucceeded = requiredValidationSucceeded;
            Configuration.TimeoutValidationSetAfter = TimeSpan.FromDays(1);
            Configuration.ValidationSetNotificationTimeout = TimeSpan.FromMinutes(20);
            Configuration.ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(1);

            ValidationSet.Created = DateTime.UtcNow - Configuration.ValidationSetNotificationTimeout - TimeSpan.FromMinutes(1);
            ValidationSet.Updated = DateTime.UtcNow - TimeSpan.FromMinutes(15);

            ValidationStorageServiceMock
                .Setup(s => s.UpdateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Callback<PackageValidationSet>(s => s.Updated = DateTime.UtcNow)
                .Returns(Task.CompletedTask);

            ValidationStorageServiceMock
                .Setup(s => s.GetValidationSetCountAsync(PackageValidatingEntity))
                .Returns(Task.FromResult(1));

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            if (expectedNotification)
            {
                MessageServiceMock
                    .Verify(m => m.SendValidationTakingTooLongMessageAsync(Package), Times.Once());
            }
            else
            {
                MessageServiceMock
                    .Verify(m => m.SendValidationTakingTooLongMessageAsync(Package), Times.Never());
            }
        }

        [Fact]
        public async Task DoesNotTakeDownAvailablePackages()
        {
            AddValidation("validation1", ValidationStatus.Failed);
            Package.PackageStatusKey = PackageStatus.Available;

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            PackageFileServiceMock.Verify(
                x => x.DeletePackageForValidationSetAsync(ValidationSet),
                Times.Once);
            PackageStateProcessorMock.Verify(
                x => x.SetStatusAsync(It.IsAny<PackageValidatingEntity>(), It.IsAny<PackageValidationSet>(), It.IsAny<PackageStatus>()),
                Times.Never);
            MessageServiceMock.Verify(
                x => x.SendValidationFailedMessageAsync(It.IsAny<Package>(), It.IsAny<PackageValidationSet>()),
                Times.Never);
            MessageServiceMock.Verify(
                x => x.SendPublishedMessageAsync(It.IsAny<Package>()),
                Times.Never);
            Assert.Equal(ValidationSetStatus.Completed, ValidationSet.ValidationSetStatus);
        }

        [Theory]
        [InlineData(2, 1, 0, ValidationStatus.Incomplete, PackageStatus.Validating)]
        [InlineData(2, 1, 1, ValidationStatus.Incomplete, PackageStatus.Available)]
        [InlineData(3, 2, 0, ValidationStatus.Incomplete, PackageStatus.Validating)]
        [InlineData(3, 2, 1, ValidationStatus.Incomplete, PackageStatus.Validating)]
        [InlineData(3, 2, 2, ValidationStatus.Incomplete, PackageStatus.Available)]
        [InlineData(2, 1, 0, ValidationStatus.Failed, PackageStatus.FailedValidation)]
        [InlineData(3, 2, 0, ValidationStatus.Failed, PackageStatus.FailedValidation)]
        [InlineData(3, 2, 1, ValidationStatus.Failed, PackageStatus.FailedValidation)]
        public async Task PrefersDbOverConfigurationForDeterminingSuccess(
            int numConfiguredValidators,
            int numDbValidators,
            int numSucceededValidators,
            ValidationStatus notSucceededStatus,
            PackageStatus expectedStatus)
        {
            for (int cfgValidatorIndex = 0; cfgValidatorIndex < numConfiguredValidators; ++cfgValidatorIndex)
            {
                Configuration.Validations.Add(new ValidationConfigurationItem
                {
                    Name = "validation" + cfgValidatorIndex,
                    TrackAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string> { }
                });
            }

            for (int dbValidatorIndex = 0; dbValidatorIndex < numDbValidators; ++dbValidatorIndex)
            {
                ValidationSet.PackageValidations.Add(new PackageValidation
                {
                    Type = "validation" + dbValidatorIndex,
                    ValidationStatus = dbValidatorIndex < numSucceededValidators ? ValidationStatus.Succeeded : notSucceededStatus,
                    PackageValidationIssues = new List<PackageValidationIssue> { }
                });
            }

            var processor = CreateProcessor();
            await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats);

            if (expectedStatus != PackageStatus.Validating)
            {
                PackageStateProcessorMock.Verify(
                    x => x.SetStatusAsync(PackageValidatingEntity, ValidationSet, expectedStatus),
                    Times.Once);
                PackageStateProcessorMock.Verify(
                    x => x.SetStatusAsync(It.IsAny<PackageValidatingEntity>(), It.IsAny<PackageValidationSet>(), expectedStatus),
                    Times.Once);
            }
            else
            {
                PackageStateProcessorMock.Verify(
                    x => x.SetStatusAsync(It.IsAny<PackageValidatingEntity>(), It.IsAny<PackageValidationSet>(), It.IsAny<PackageStatus>()),
                    Times.Never);
            }
        }

        [Fact]
        public async Task PackageStillBecomesAvailableIfPublishedMessageFails()
        {
            var exception = new Exception("Something baaad happened");

            MessageServiceMock
                .Setup(ms => ms.SendPublishedMessageAsync(It.IsAny<Package>()))
                .Throws(exception);

            Package.PackageStatusKey = PackageStatus.Validating;

            var processor = CreateProcessor();
            var thrownException = await Record.ExceptionAsync(
                async () => await processor.ProcessValidationOutcomeAsync(ValidationSet, PackageValidatingEntity, ProcessorStats));

            Assert.NotNull(thrownException);
            PackageStateProcessorMock.Verify(
                x => x.SetStatusAsync(PackageValidatingEntity, ValidationSet, PackageStatus.Available),
                Times.Once);
            PackageFileServiceMock.Verify(
                x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
        }

        public ValidationOutcomeProcessorFacts()
        {
            ValidationStorageServiceMock = new Mock<IValidationStorageService>();
            ValidationEnqueuerMock = new Mock<IPackageValidationEnqueuer>();
            PackageStateProcessorMock = new Mock<IStatusProcessor<Package>>();
            PackageFileServiceMock = new Mock<IValidationFileService>();
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            MessageServiceMock = new Mock<IMessageService<Package>>();
            TelemetryServiceMock = new Mock<ITelemetryService>();
            LoggerMock = new Mock<ILogger<ValidationOutcomeProcessor<Package>>>();

            Configuration = new ValidationConfiguration();
            Configuration.Validations = new List<ValidationConfigurationItem>();
            Package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "package" },
                Version = "1.2.3.456",
                NormalizedVersion = "1.2.3",
                PackageStatusKey = PackageStatus.Validating
            };
            Package.PackageRegistration.Packages.Add(Package);

            ValidationSet = new PackageValidationSet();
            ValidationSet.PackageValidations = new List<PackageValidation>();

            ValidationSet.PackageId = Package.PackageRegistration.Id;
            ValidationSet.PackageNormalizedVersion = Package.NormalizedVersion;
            ValidationSet.ValidationTrackingId = Guid.NewGuid();
            ValidationSet.Created = DateTime.UtcNow - TimeSpan.FromHours(3);
            ValidationSet.Updated = ValidationSet.Created + TimeSpan.FromHours(1);
            ValidationSet.ValidationSetStatus = ValidationSetStatus.InProgress;

            ProcessorStats = new ValidationSetProcessorResult();

            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(Configuration);

            PackageValidatingEntity = new PackageValidatingEntity(Package);
        }

        protected ValidationOutcomeProcessor<Package> CreateProcessor()
        {
            return new ValidationOutcomeProcessor<Package>(
                ValidationStorageServiceMock.Object,
                ValidationEnqueuerMock.Object,
                PackageStateProcessorMock.Object,
                PackageFileServiceMock.Object,
                ConfigurationAccessorMock.Object,
                MessageServiceMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);
        }

        protected Mock<IValidationStorageService> ValidationStorageServiceMock { get; }
        protected Mock<IStatusProcessor<Package>> PackageStateProcessorMock { get; }
        protected Mock<IValidationFileService> PackageFileServiceMock { get; }
        protected Mock<IPackageValidationEnqueuer> ValidationEnqueuerMock { get; }
        protected Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        protected Mock<IMessageService<Package>> MessageServiceMock { get; }
        public Mock<ITelemetryService> TelemetryServiceMock { get; }
        protected Mock<ILogger<ValidationOutcomeProcessor<Package>>> LoggerMock { get; }
        protected ValidationConfiguration Configuration { get; }
        protected PackageValidationSet ValidationSet { get; }
        protected Package Package { get; }
        protected ValidationSetProcessorResult ProcessorStats { get; }

        protected PackageValidatingEntity PackageValidatingEntity { get; }

        private void AddValidation(
            string validationName,
            ValidationStatus validationStatus,
            ValidationFailureBehavior failureBehavior = ValidationFailureBehavior.MustSucceed,
            DateTime? validationStart = null,
            TimeSpan? trackAfter = null)
        {
            ValidationSet.PackageValidations.Add(new PackageValidation
            {
                Type = validationName,
                Started = validationStart,
                ValidationStatus = validationStatus,
                PackageValidationIssues = new List<PackageValidationIssue> { },
            });
            Configuration.Validations.Add(new ValidationConfigurationItem
            {
                Name = validationName,
                TrackAfter = trackAfter ?? TimeSpan.FromDays(1),
                RequiredValidations = new List<string> { },
                ShouldStart = true,
                FailureBehavior = failureBehavior
            });
        }

        private List<string> RecordOperationOrder()
        {
            var operations = new List<string>();

            PackageStateProcessorMock
                .Setup(x => x.SetStatusAsync(It.IsAny<IValidatingEntity<Package>>(), It.IsAny<PackageValidationSet>(), It.IsAny<PackageStatus>()))
                .Returns(Task.CompletedTask)
                .Callback(() => operations.Add(nameof(IStatusProcessor<Package>.SetStatusAsync)));
            ValidationStorageServiceMock
                .Setup(x => x.UpdateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns(Task.CompletedTask)
                .Callback(() => operations.Add(nameof(IValidationStorageService.UpdateValidationSetAsync)));
            MessageServiceMock
                .Setup(x => x.SendPublishedMessageAsync(It.IsAny<Package>()))
                .Returns(Task.CompletedTask)
                .Callback(() => operations.Add(nameof(IMessageService<Package>.SendPublishedMessageAsync)));
            MessageServiceMock
                .Setup(x => x.SendValidationFailedMessageAsync(It.IsAny<Package>(), It.IsAny<PackageValidationSet>()))
                .Returns(Task.CompletedTask)
                .Callback(() => operations.Add(nameof(IMessageService<Package>.SendValidationFailedMessageAsync)));
            TelemetryServiceMock
                .Setup(x => x.TrackTotalValidationDuration(It.IsAny<TimeSpan>(), It.IsAny<bool>()))
                .Callback(() => operations.Add(nameof(ITelemetryService.TrackTotalValidationDuration)));
            PackageFileServiceMock
                .Setup(x => x.DeletePackageForValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns(Task.CompletedTask)
                .Callback(() => operations.Add(nameof(IValidationFileService.DeletePackageForValidationSetAsync)));
            return operations;
        }
    }
}

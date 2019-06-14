// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationOutcomeProcessor<T> : IValidationOutcomeProcessor<T> where T : class, IEntity
    {
        private readonly IValidationStorageService _validationStorageService;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly IStatusProcessor<T> _packageStateProcessor;
        private readonly IValidationFileService _packageFileService;
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly Dictionary<string, ValidationConfigurationItem> _validationConfigurationsByName;
        private readonly IMessageService<T> _messageService;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ValidationOutcomeProcessor<T>> _logger;

        public ValidationOutcomeProcessor(
            IValidationStorageService validationStorageService,
            IPackageValidationEnqueuer validationEnqueuer,
            IStatusProcessor<T> validatedPackageProcessor,
            IValidationFileService packageFileService,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            IMessageService<T> messageService,
            ITelemetryService telemetryService,
            ILogger<ValidationOutcomeProcessor<T>> logger)
        {
            _validationStorageService = validationStorageService ?? throw new ArgumentNullException(nameof(validationStorageService));
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _packageStateProcessor = validatedPackageProcessor ?? throw new ArgumentNullException(nameof(validatedPackageProcessor));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            if (validationConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(validationConfigurationAccessor));
            }
            _validationConfiguration = validationConfigurationAccessor.Value 
                ?? throw new ArgumentException($"The {nameof(validationConfigurationAccessor)}.Value property cannot be null",
                    nameof(validationConfigurationAccessor));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _validationConfigurationsByName = _validationConfiguration.Validations.ToDictionary(v => v.Name);
        }

        public async Task ProcessValidationOutcomeAsync(PackageValidationSet validationSet, IValidatingEntity<T> validatingEntity, ValidationSetProcessorResult currentCallStats)
        {
            var failedValidations = GetFailedValidations(validationSet);

            if (failedValidations.Any())
            {
                _logger.LogWarning("Some validations failed for package {PackageId} {PackageVersion}, validation set {ValidationSetId}: {FailedValidations}",
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId,
                    failedValidations.Select(x => x.Type).ToList());

                // The only way we can move to the failed validation state is if the package is currently in the
                // validating state. This has a beneficial side effect of only sending a failed validation email to the
                // customer when the package first moves to the failed validation state. If an admin comes along and
                // revalidates the package and the package fails validation again, we don't want another email going
                // out since that would be noisy for the customer.                
                if (validatingEntity.Status == PackageStatus.Validating)
                {
                    await _packageStateProcessor.SetStatusAsync(validatingEntity, validationSet, PackageStatus.FailedValidation);

                    await MarkValidationSetAsCompletedAsync(validationSet);

                    await _messageService.SendValidationFailedMessageAsync(validatingEntity.EntityRecord, validationSet);
                }
                else
                {
                    // The case when validation fails while PackageStatus not validating is the case of 
                    // manual revalidation. In this case we don't want to take package down automatically
                    // and let the person who requested revalidation to decide how to proceed. Ops will be
                    // alerted by failed validation monitoring.
                    _logger.LogInformation("Package {PackageId} {PackageVersion} was {PackageStatus} when validation set {ValidationSetId} failed. Will not mark it as failed.",
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validatingEntity.Status,
                        validationSet.ValidationTrackingId);

                    await MarkValidationSetAsCompletedAsync(validationSet);
                }

                TrackValidationSetCompletion(validationSet, isSuccess: false);

                await _packageFileService.DeletePackageForValidationSetAsync(validationSet);
            }
            else if (AllRequiredValidationsSucceeded(validationSet))
            {
                _logger.LogInformation("All validations are complete for the package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId);

                var fromStatus = validatingEntity.Status;

                // Always set the package status to available so that processors can have a chance to fix packages
                // that are already available. Processors should no-op when their work is already done, so the
                // modification of an already available package should be rare. The most common case for this is if
                // the processor has never been run on a package that was published before the processor was
                // implemented. In this case, the processor has to play catch-up.
                await _packageStateProcessor.SetStatusAsync(validatingEntity, validationSet, PackageStatus.Available);

                var areOptionalValidationsRunning = AreOptionalValidationsRunning(validationSet);
                if (!areOptionalValidationsRunning)
                {
                    await MarkValidationSetAsCompletedAsync(validationSet);
                }

                // Only send the email when first transitioning into the Available state.
                if (fromStatus != PackageStatus.Available)
                {
                    await _messageService.SendPublishedMessageAsync(validatingEntity.EntityRecord);
                }

                if (currentCallStats.AnyRequiredValidationSucceeded)
                {
                    TrackValidationSetCompletion(validationSet, isSuccess: true);
                }

                if (areOptionalValidationsRunning)
                {
                    await ScheduleCheckIfNotTimedOut(validationSet, validatingEntity, tooLongNotificationAllowed: false);
                }
                else
                {
                    await _packageFileService.DeletePackageForValidationSetAsync(validationSet);
                }
            }
            else
            {
                await ScheduleCheckIfNotTimedOut(validationSet, validatingEntity, tooLongNotificationAllowed: true);
            }
        }

        private async Task MarkValidationSetAsCompletedAsync(PackageValidationSet validationSet)
        {
            // Move the validation set to the completed status. This operation is done using optimistic concurrency
            // meaning if another thread is processing this same validation set at the same time, one thread will win
            // and one thread will fail. In other words, subsequent steps will be executed at most one time. From
            validationSet.ValidationSetStatus = ValidationSetStatus.Completed;
            await _validationStorageService.UpdateValidationSetAsync(validationSet);
        }

        private void TrackValidationSetCompletion(PackageValidationSet validationSet, bool isSuccess)
        {
            _logger.LogInformation("Done processing {PackageId} {PackageVersion} {ValidationSetId} with IsSuccess = {IsSuccess}.",
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validationSet.ValidationTrackingId,
                isSuccess);

            TrackTotalValidationDuration(validationSet, isSuccess);
        }

        private ValidationConfigurationItem GetValidationConfigurationItemByName(string name)
        {
            _validationConfigurationsByName.TryGetValue(name, out var item);

            return item;
        }

        private void TrackTotalValidationDuration(PackageValidationSet validationSet, bool isSuccess)
        {
            _telemetryService.TrackTotalValidationDuration(
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validationSet.ValidationTrackingId,
                DateTime.UtcNow - validationSet.Created,
                isSuccess);
        }

        private bool AllRequiredValidationsSucceeded(PackageValidationSet packageValidationSet)
        {
            return packageValidationSet
                .PackageValidations
                .All(pv => pv.ValidationStatus == ValidationStatus.Succeeded
                    || GetValidationConfigurationItemByName(pv.Type)?.FailureBehavior == ValidationFailureBehavior.AllowedToFail);
        }

        private bool AreOptionalValidationsRunning(PackageValidationSet packageValidationSet)
        {
            return packageValidationSet
                .PackageValidations
                .Any(pv => pv.ValidationStatus == ValidationStatus.Incomplete
                    && GetValidationConfigurationItemByName(pv.Type)?.FailureBehavior == ValidationFailureBehavior.AllowedToFail);
        }

        private List<PackageValidation> GetFailedValidations(PackageValidationSet packageValidationSet)
        {
            return packageValidationSet
                .PackageValidations
                .Where(v => v.ValidationStatus == ValidationStatus.Failed)
                .Where(v => GetValidationConfigurationItemByName(v.Type)?.FailureBehavior == ValidationFailureBehavior.MustSucceed)
                .ToList();
        }

        private List<PackageValidation> GetIncompleteTimedOutValidations(PackageValidationSet packageValidationSet)
        {
            bool IsPackageValidationTimedOut(PackageValidation validation)
            {
                var config = GetValidationConfigurationItemByName(validation.Type);
                var duration = DateTime.UtcNow - validation.Started;

                return duration > config?.TrackAfter;
            }

            return packageValidationSet
                .PackageValidations
                .Where(v => v.ValidationStatus == ValidationStatus.Incomplete)
                .Where(IsPackageValidationTimedOut)
                .ToList();
        }

        private async Task<TimeSpan> UpdateValidationDurationAsync(PackageValidationSet validationSet, IValidatingEntity<T> validatingEntity, bool tooLongNotificationAllowed)
        {
            // There are no failed validations and some validations are still in progress. Update
            // the validation set's Updated field and send a notice if the validation set is taking
            // too long to complete.
            var previousUpdateTime = validationSet.Updated;

            await _validationStorageService.UpdateValidationSetAsync(validationSet);

            var validationSetDuration = validationSet.Updated - validationSet.Created;
            var previousDuration = previousUpdateTime - validationSet.Created;

            // Only send a "validating taking too long" notice once. This is ensured by verifying this is
            // the package's first validation set and that this is the first time the validation set duration
            // is greater than the configured threshold. Service Bus message duplication for a single validation
            // set will not cause multiple notices to be sent due to the row version on PackageValidationSet.
            if (tooLongNotificationAllowed &&
                validatingEntity.Status == PackageStatus.Validating &&
                validationSetDuration > _validationConfiguration.ValidationSetNotificationTimeout &&
                previousDuration <= _validationConfiguration.ValidationSetNotificationTimeout &&
                await _validationStorageService.GetValidationSetCountAsync(validatingEntity) == 1)
            {
                _logger.LogWarning("Sending message that validation set {ValidationTrackingId} for package {PackageId} {PackageVersion} is taking too long",
                    validationSet.ValidationTrackingId,
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion);

                await _messageService.SendValidationTakingTooLongMessageAsync(validatingEntity.EntityRecord);
                _telemetryService.TrackSentValidationTakingTooLongMessage(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId);
            }

            // Track any validations that are past their expected thresholds.
            var timedOutValidations = GetIncompleteTimedOutValidations(validationSet);

            if (timedOutValidations.Any())
            {
                foreach (var validation in timedOutValidations)
                {
                    var duration = DateTime.UtcNow - validation.Started;

                    _logger.LogWarning("Validation {Validation} for package {PackageId} {PackageVersion} is past its expected duration after {Duration}",
                        validation.Type,
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        duration);

                    _telemetryService.TrackValidatorTimeout(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId, validation.Type);
                }
            }

            return validationSetDuration;
        }

        private async Task ScheduleCheckIfNotTimedOut(PackageValidationSet validationSet, IValidatingEntity<T> validatingEntity, bool tooLongNotificationAllowed)
        {
            var validationSetDuration = await UpdateValidationDurationAsync(validationSet, validatingEntity, tooLongNotificationAllowed);

            // Schedule another check if we haven't reached the validation set timeout yet.
            if (validationSetDuration <= _validationConfiguration.TimeoutValidationSetAfter)
            {
                var messageData = new PackageValidationMessageData(
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId,
                    validationSet.ValidatingType,
                    entityKey: validationSet.PackageKey);
                var postponeUntil = DateTimeOffset.UtcNow + _validationConfiguration.ValidationMessageRecheckPeriod;

                await _validationEnqueuer.StartValidationAsync(messageData, postponeUntil);
            }
            else
            {
                _logger.LogWarning("Abandoning checking status of validation set {ValidationTrackingId} for " +
                    "package {PackageId} {PackageVersion} because it took too long (Duration: {Duration}, CutOffDuration: {CutOffDuration})",
                    validationSet.ValidationTrackingId,
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSetDuration,
                    _validationConfiguration.TimeoutValidationSetAfter);
                _telemetryService.TrackValidationSetTimeout(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId);
            }
        }
    }
}

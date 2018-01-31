// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationOutcomeProcessor : IValidationOutcomeProcessor
    {
        private readonly ICorePackageService _galleryPackageService;
        private readonly ICorePackageFileService _packageFileService;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly IMessageService _messageService;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ValidationOutcomeProcessor> _logger;

        public ValidationOutcomeProcessor(
            ICorePackageService galleryPackageService,
            ICorePackageFileService packageFileService,
            IPackageValidationEnqueuer validationEnqueuer,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            IMessageService messageService,
            ITelemetryService telemetryService,
            ILogger<ValidationOutcomeProcessor> logger)
        {
            _galleryPackageService = galleryPackageService ?? throw new ArgumentNullException(nameof(galleryPackageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
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
        }

        public async Task ProcessValidationOutcomeAsync(PackageValidationSet validationSet, Package package)
        {
            var validations = _validationConfiguration.Validations.ToDictionary(v => v.Name);
            ValidationConfigurationItem GetValidationConfigurationItem(string validationName)
            {
                if (validations.TryGetValue(validationName, out ValidationConfigurationItem validationConfigurationItem))
                {
                    return validationConfigurationItem;
                }
                return null;
            }

            if (AnyValidationFailed(validationSet, GetValidationConfigurationItem))
            {
                _logger.LogWarning("Some validations failed for package {PackageId} {PackageVersion}, validation set {ValidationSetId}: {FailedValidations}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId,
                    GetFailedValidations(validationSet, GetValidationConfigurationItem));

                // The only way we can move to the failed validation state is if the package is currently in the
                // validating state. This has a beneficial side effect of only sending a failed validation email to the
                // customer when the package first moves to the failed validation state. If an admin comes along and
                // revalidates the package and the package fails validation again, we don't want another email going
                // out since that would be noisy for the customer.                
                if (package.PackageStatusKey == PackageStatus.Validating)
                {
                    await UpdatePackageStatusAsync(package, PackageStatus.FailedValidation);

                    var issuesExistAndAllPackageSigned = validationSet
                        .PackageValidations
                        .SelectMany(pv => pv.PackageValidationIssues)
                        .Select(pvi => pvi.IssueCode == ValidationIssueCode.PackageIsSigned)
                        .DefaultIfEmpty(false)
                        .All(v => v);

                    if (issuesExistAndAllPackageSigned)
                    {
                        _messageService.SendPackageSignedValidationFailedMessage(package);
                    }
                    else
                    {
                        _messageService.SendPackageValidationFailedMessage(package);
                    }
                }
                else
                {
                    // The case when validation fails while PackageStatus not validating is the case of 
                    // manual revalidation. In this case we don't want to take package down automatically
                    // and let the person who requested revalidation to decide how to proceed. Ops will be
                    // alerted by failed validation monitoring.
                    _logger.LogInformation("Package {PackageId} {PackageVersion} was {PackageStatus} when validation set {ValidationSetId} failed. Will not mark it as failed.",
                        package.PackageRegistration.Id,
                        package.NormalizedVersion,
                        package.PackageStatusKey,
                        validationSet.ValidationTrackingId);
                }

                TrackTotalValidationDuration(validationSet, isSuccess: false);
            }
            else if (AllValidationsSucceeded(validationSet, GetValidationConfigurationItem))
            {
                _logger.LogInformation("All validations are complete for the package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId);
                if (package.PackageStatusKey != PackageStatus.Available)
                {
                    await MoveFileToPublicStorageAndMarkPackageAsAvailable(validationSet, package);
                }
                else
                {
                    _logger.LogInformation("Package {PackageId} {PackageVersion} {ValidationSetId} was already available, not going to copy data and update DB",
                        package.PackageRegistration.Id,
                        package.NormalizedVersion,
                        validationSet.ValidationTrackingId);
                }
                _logger.LogInformation("Done processing {PackageId} {PackageVersion} {ValidationSetId}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId);

                TrackTotalValidationDuration(validationSet, isSuccess: true);
            }
            else
            {
                // No failed validations and some validations are still in progress.
                // Scheduling another check
                var messageData = new PackageValidationMessageData(package.PackageRegistration.Id, package.Version, validationSet.ValidationTrackingId);
                await _validationEnqueuer.StartValidationAsync(messageData, DateTimeOffset.UtcNow + _validationConfiguration.ValidationMessageRecheckPeriod);
            }
        }

        private void TrackTotalValidationDuration(PackageValidationSet validationSet, bool isSuccess)
        {
            _telemetryService.TrackTotalValidationDuration(
                DateTime.UtcNow - validationSet.Created,
                isSuccess);
        }

        private async Task MoveFileToPublicStorageAndMarkPackageAsAvailable(PackageValidationSet validationSet, Package package)
        {
            _logger.LogInformation("Copying .nupkg to public storage for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                validationSet.ValidationTrackingId);
            var packageStream = await _packageFileService.DownloadValidationPackageFileAsync(package);
            await _packageFileService.SavePackageFileAsync(package, packageStream);

            try
            {
                _logger.LogInformation("Marking package {PackageId} {PackageVersion}, validation set {ValidationSetId} as {PackageStatus} in DB",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId,
                    PackageStatus.Available);

                await UpdatePackageStatusAsync(package, PackageStatus.Available);

                _messageService.SendPackagePublishedMessage(package);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    Error.UpdatingPackageDbStatusFailed,
                    e,
                    "Failed to update package status in Gallery Db. Package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId);

                await _packageFileService.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version);

                throw;
            }

            _logger.LogInformation("Deleting from the source for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                validationSet.ValidationTrackingId);
            await _packageFileService.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version);
        }

        private bool AllValidationsSucceeded(
            PackageValidationSet packageValidationSet,
            Func<string, ValidationConfigurationItem> getValidationConfigurationItem)
        {
            return packageValidationSet
                .PackageValidations
                .All(pv => pv.ValidationStatus == ValidationStatus.Succeeded
                    || getValidationConfigurationItem(pv.Type)?.FailureBehavior == ValidationFailureBehavior.AllowedToFail);
        }

        private IEnumerable<PackageValidation> GetFailedValidations(
            PackageValidationSet packageValidationSet,
            Func<string, ValidationConfigurationItem> getValidationConfigurationItem)
        {
            return packageValidationSet
                .PackageValidations
                .Where(v => v.ValidationStatus == ValidationStatus.Failed
                    && getValidationConfigurationItem(v.Type)?.FailureBehavior == ValidationFailureBehavior.MustSucceed);
        }

        private bool AnyValidationFailed(
            PackageValidationSet packageValidationSet,
            Func<string, ValidationConfigurationItem> getValidationConfigurationItem)
        {
            return GetFailedValidations(packageValidationSet, getValidationConfigurationItem).Any();
        }

        private async Task UpdatePackageStatusAsync(Package package, PackageStatus toStatus)
        {
            var fromStatus = package.PackageStatusKey;

            await _galleryPackageService.UpdatePackageStatusAsync(package, toStatus, commitChanges: true);

            if (fromStatus != toStatus)
            {
                _telemetryService.TrackPackageStatusChange(fromStatus, toStatus);
            }
        }
    }
}

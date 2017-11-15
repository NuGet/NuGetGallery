// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationOutcomeProcessor : IValidationOutcomeProcessor
    {
        private readonly ICorePackageService _galleryPackageService;
        private readonly ICorePackageFileService _packageFileService;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly ValidationConfiguration _validationConfiguration;
        private readonly ILogger<ValidationOutcomeProcessor> _logger;

        public ValidationOutcomeProcessor(
            ICorePackageService galleryPackageService,
            ICorePackageFileService packageFileService,
            IPackageValidationEnqueuer validationEnqueuer,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
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
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessValidationOutcomeAsync(PackageValidationSet validationSet, Package package)
        {
            if (AnyValidationFailed(validationSet))
            {
                _logger.LogWarning("Some validations failed for package {PackageId} {PackageVersion}, validation set {ValidationSetId}: {FailedValidations}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId,
                    GetFailedValidations(validationSet));

                if (package.PackageStatusKey != PackageStatus.Available)
                {
                    await _galleryPackageService.UpdatePackageStatusAsync(package, PackageStatus.FailedValidation);
                }
                else
                {
                    // The case when validation fails while PackageStatus is Available is the case of 
                    // manual revalidation. In this case we don't want to take package down automatically
                    // and let the person who requested revalidation to decide how to proceed. User will be
                    // alerted by failed validation monitoring.
                    _logger.LogInformation("Package {PackageId} {PackageVersion} was available when validation set {ValidationSetId} failed. Will not mark it as failed",
                        package.PackageRegistration.Id,
                        package.NormalizedVersion,
                        validationSet.ValidationTrackingId);
                }
            }
            else if (AllValidationsSucceeded(validationSet))
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
            }
            else
            {
                // No failed validations and some validations are still in progress.
                // Scheduling another check
                var messageData = new PackageValidationMessageData(package.PackageRegistration.Id, package.Version, validationSet.ValidationTrackingId);
                await _validationEnqueuer.StartValidationAsync(messageData, DateTimeOffset.UtcNow + _validationConfiguration.ValidationMessageRecheckPeriod);
            }
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
                await _galleryPackageService.UpdatePackageStatusAsync(package, PackageStatus.Available, commitChanges: true);
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

        private bool AllValidationsSucceeded(PackageValidationSet packageValidationSet)
        {
            var completeValidations = new HashSet<string>(packageValidationSet
                .PackageValidations
                .Where(v => v.ValidationStatus == ValidationStatus.Succeeded)
                .Select(v => v.Type));
            var requiredValidations = _validationConfiguration
                .Validations
                .Select(v => v.Name);

            return completeValidations.IsSupersetOf(requiredValidations);
        }

        private IEnumerable<PackageValidation> GetFailedValidations(PackageValidationSet packageValidationSet)
        {
            return packageValidationSet.PackageValidations.Where(v => v.ValidationStatus == ValidationStatus.Failed);
        }

        private bool AnyValidationFailed(PackageValidationSet packageValidationSet)
        {
            return GetFailedValidations(packageValidationSet).Any();
        }
    }
}

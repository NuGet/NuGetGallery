// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageStatusProcessor : IPackageStatusProcessor
    {
        private readonly ICorePackageService _galleryPackageService;
        private readonly IValidationPackageFileService _packageFileService;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<PackageStatusProcessor> _logger;

        public PackageStatusProcessor(
            ICorePackageService galleryPackageService,
            IValidationPackageFileService packageFileService,
            ITelemetryService telemetryService,
            ILogger<PackageStatusProcessor> logger)
        {
            _galleryPackageService = galleryPackageService ?? throw new ArgumentNullException(nameof(galleryPackageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task SetPackageStatusAsync(
            Package package,
            PackageValidationSet validationSet,
            PackageStatus packageStatus)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            if (package.PackageStatusKey == PackageStatus.Deleted)
            {
                throw new ArgumentException(
                    $"A package in the {nameof(PackageStatus.Deleted)} state cannot be processed.",
                    nameof(package));
            }

            if (package.PackageStatusKey == PackageStatus.Available &&
                packageStatus == PackageStatus.FailedValidation)
            {
                throw new ArgumentException(
                    $"A package cannot transition from {nameof(PackageStatus.Available)} to {nameof(PackageStatus.FailedValidation)}.",
                    nameof(packageStatus));
            }

            switch (packageStatus)
            {
                case PackageStatus.Available:
                    return MakePackageAvailableAsync(package, validationSet);
                case PackageStatus.FailedValidation:
                    return MakePackageFailedValidationAsync(package, validationSet);
                default:
                    throw new ArgumentException(
                        $"A package can only transition to the {nameof(PackageStatus.Available)} or " +
                        $"{nameof(PackageStatus.FailedValidation)} states.", nameof(packageStatus));
            }
        }

        private Task MakePackageFailedValidationAsync(Package package, PackageValidationSet validationSet)
        {
            return UpdatePackageStatusAsync(package, PackageStatus.FailedValidation);
        }

        private async Task MakePackageAvailableAsync(Package package, PackageValidationSet validationSet)
        {
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

                if (!await _packageFileService.DoesPackageFileExistAsync(package))
                {
                    var validationPackageAvailable = await _packageFileService.DoesValidationPackageFileExistAsync(package);

                    _logger.LogWarning("Package {PackageId} {PackageVersion} is marked as available, but does not exist " +
                        "in public container. Does package exist in validation container: {ExistsInValidation}",
                        package.PackageRegistration.Id,
                        package.NormalizedVersion,
                        validationPackageAvailable);

                    // Report missing package, don't try to fix up anything. This shouldn't happen and needs an investigation.
                    _telemetryService.TrackMissingNupkgForAvailablePackage(
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validationSet.ValidationTrackingId.ToString());
                }
            }
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

        private async Task MoveFileToPublicStorageAndMarkPackageAsAvailable(PackageValidationSet validationSet, Package package)
        {
            _logger.LogInformation("Copying .nupkg to public storage for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                validationSet.ValidationTrackingId);

            bool copied;
            if (await _packageFileService.DoesValidationSetPackageExistAsync(validationSet))
            {
                copied = await CopyAsync(
                    validationSet,
                    package,
                    x => _packageFileService.CopyValidationSetPackageToPackageFileAsync(x));
            }
            else
            {
                _logger.LogInformation(
                    "The package specific to the validation set does not exist. Falling back to the validation " +
                    "container for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId);

                copied = await CopyAsync(
                    validationSet,
                    package,
                    x => _packageFileService.CopyValidationPackageToPackageFileAsync(x.PackageId, x.PackageNormalizedVersion));
            }

            _logger.LogInformation("Marking package {PackageId} {PackageVersion}, validation set {ValidationSetId} as {PackageStatus} in DB",
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                validationSet.ValidationTrackingId,
                PackageStatus.Available);

            try
            {
                await UpdatePackageStatusAsync(package, PackageStatus.Available);
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

                // If this execution was not the one to copy the package, then don't delete the package on failure.
                // This prevents the (unlikely) case where two actors attempt the DB update, one suceeds and one fails.
                // We don't want an available package record with nothing in the packages container!
                if (copied)
                {
                    await _packageFileService.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version);
                }

                throw;
            }
            
            _logger.LogInformation("Deleting from the source for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                validationSet.ValidationTrackingId);

            await _packageFileService.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version);
        }

        private async Task<bool> CopyAsync(PackageValidationSet validationSet, Package package, Func<PackageValidationSet, Task> copyAsync)
        {
            try
            {
                await copyAsync(validationSet);

                return true;
            }
            catch (InvalidOperationException)
            {
                // The package already exists in the packages container. This can happen if the DB commit below fails
                // and this flow is retried. We assume that the package content has not changed. Today there is no way
                // for the content to change. Hard deletes (the one way a package ID and version can get different
                // content) delete from both the packages and validating container so this can't be a mismatch.
                _logger.LogInformation(
                    "Package already exists in packages container for {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId);

                return false;
            }
        }
    }
}

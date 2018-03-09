// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageStatusProcessor : IPackageStatusProcessor
    {
        private readonly ICorePackageService _galleryPackageService;
        private readonly IValidationPackageFileService _packageFileService;
        private readonly IValidatorProvider _validatorProvider;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<PackageStatusProcessor> _logger;

        public PackageStatusProcessor(
            ICorePackageService galleryPackageService,
            IValidationPackageFileService packageFileService,
            IValidatorProvider validatorProvider,
            ITelemetryService telemetryService,
            ILogger<PackageStatusProcessor> logger)
        {
            _galleryPackageService = galleryPackageService ?? throw new ArgumentNullException(nameof(galleryPackageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _validatorProvider = validatorProvider ?? throw new ArgumentNullException(nameof(validatorProvider));
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

            // If the validation set contains any processors, we must use the copy of the package that is specific to
            // this validation set. We can't use the original validation package because it does not have any of the
            // changes that the processors made. If the validation set package does not exist for some reason and there
            // are processors in the validation set, this indicates a bug and an exception will be thrown by the copy
            // operation below. This will cause the validation queue message to eventually dead-letter at which point
            // the on-call person should investigate.
            bool copied;
            if (validationSet.PackageValidations.Any(x => _validatorProvider.IsProcessor(x.Type)) ||
                await _packageFileService.DoesValidationSetPackageExistAsync(validationSet))
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

            // Use whatever package made it into the packages container. This is what customers will consume so the DB
            // record must match.
            using (var packageStream = await _packageFileService.DownloadPackageFileToDiskAsync(package))
            {
                var stopwatch = Stopwatch.StartNew();
                var hash = CryptographyService.GenerateHash(packageStream, CoreConstants.Sha512HashAlgorithmId);
                _telemetryService.TrackDurationToHashPackage(
                    stopwatch.Elapsed,
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    CoreConstants.Sha512HashAlgorithmId,
                    packageStream.GetType().FullName);

                var streamMetadata = new PackageStreamMetadata
                {
                    Size = packageStream.Length,
                    Hash = hash,
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                };

                // We don't immediately commit here. Later, we will commit these changes as well as the new package
                // status as part of the same transaction.
                if (streamMetadata.Size != package.PackageFileSize
                    || streamMetadata.Hash != package.Hash
                    || streamMetadata.HashAlgorithm != package.HashAlgorithm)
                {
                    await _galleryPackageService.UpdatePackageStreamMetadataAsync(
                        package,
                        streamMetadata,
                        commitChanges: false);
                }
            }

            _logger.LogInformation("Marking package {PackageId} {PackageVersion}, validation set {ValidationSetId} as {PackageStatus} in DB",
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                validationSet.ValidationTrackingId,
                PackageStatus.Available);

            try
            {
                // Make the package available and commit any other pending changes (e.g. updated hash).
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
                // This prevents a missing passing in the (unlikely) case where two actors attempt the DB update, one
                // succeeds and one fails. We don't want an available package record with nothing in the packages
                // container!
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
                // and this flow is retried or another validation set for the package completed first. Either way, we
                // will later attempt to use the hash from the package in the packages container (the destination).
                // In other words, we don't care which copy wins, but the DB record must match the package that ends
                // up in the packages container.
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

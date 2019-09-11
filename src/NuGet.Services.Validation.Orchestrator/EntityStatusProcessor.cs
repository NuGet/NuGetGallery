// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public abstract class EntityStatusProcessor<T> : IStatusProcessor<T> where T : class, IEntity
    {
        protected readonly IEntityService<T> _galleryPackageService;
        protected readonly IValidationFileService _packageFileService;
        protected readonly IValidatorProvider _validatorProvider;
        protected readonly ITelemetryService _telemetryService;
        protected readonly ILogger<EntityStatusProcessor<T>> _logger;

        public EntityStatusProcessor(
            IEntityService<T> galleryPackageService,
            IValidationFileService packageFileService,
            IValidatorProvider validatorProvider,
            ITelemetryService telemetryService,
            ILogger<EntityStatusProcessor<T>> logger)
        {
            _galleryPackageService = galleryPackageService ?? throw new ArgumentNullException(nameof(galleryPackageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _validatorProvider = validatorProvider ?? throw new ArgumentNullException(nameof(validatorProvider));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task SetStatusAsync(
            IValidatingEntity<T> validatingEntity,
            PackageValidationSet validationSet,
            PackageStatus status)
        {
            if (validatingEntity == null)
            {
                throw new ArgumentNullException(nameof(validatingEntity));
            }

            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            if (validatingEntity.Status == PackageStatus.Deleted)
            {
                throw new ArgumentException(
                    $"A package in the {nameof(PackageStatus.Deleted)} state cannot be processed.",
                    nameof(validatingEntity));
            }

            if (validatingEntity.Status == PackageStatus.Available &&
                status == PackageStatus.FailedValidation)
            {
                throw new ArgumentException(
                    $"A package cannot transition from {nameof(PackageStatus.Available)} to {nameof(PackageStatus.FailedValidation)}.",
                    nameof(status));
            }

            switch (status)
            {
                case PackageStatus.Available:
                    return MakePackageAvailableAsync(validatingEntity, validationSet);
                case PackageStatus.FailedValidation:
                    return MakePackageFailedValidationAsync(validatingEntity, validationSet);
                default:
                    throw new ArgumentException(
                        $"A package can only transition to the {nameof(PackageStatus.Available)} or " +
                        $"{nameof(PackageStatus.FailedValidation)} states.", nameof(status));
            }
        }

        protected virtual async Task MakePackageFailedValidationAsync(IValidatingEntity<T> validatingEntity, PackageValidationSet validationSet)
        {
            var fromStatus = validatingEntity.Status;

            await _galleryPackageService.UpdateStatusAsync(validatingEntity.EntityRecord, PackageStatus.FailedValidation, commitChanges: true);

            if (fromStatus != PackageStatus.FailedValidation)
            {
                _telemetryService.TrackPackageStatusChange(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId, fromStatus, PackageStatus.FailedValidation);
            }
        }

        protected virtual async Task MakePackageAvailableAsync(IValidatingEntity<T> validatingEntity, PackageValidationSet validationSet)
        {
            // 1) Operate on blob storage, and update the metadata.
            var packageStreamMetadataAndCopyStatusWrapper = await UpdatePublicPackageAsync(validationSet);

            // 2) Update the package's blob properties in the public blob storage container.
            await _packageFileService.UpdatePackageBlobPropertiesAsync(validationSet);

            // 2.5) Allow descendants to do their own things before we update the database
            await OnBeforeUpdateDatabaseToMakePackageAvailable(validatingEntity, validationSet);

            // 3) Operate on the database.
            var fromStatus = await MarkPackageAsAvailableAsync(validationSet,
                validatingEntity,
                packageStreamMetadataAndCopyStatusWrapper.PackageStreamMetadata,
                packageStreamMetadataAndCopyStatusWrapper.Copied);

            // 4) Emit telemetry and clean up.
            if (fromStatus != PackageStatus.Available)
            {
                _telemetryService.TrackPackageStatusChange(validationSet.PackageId, validationSet.PackageNormalizedVersion, validationSet.ValidationTrackingId, fromStatus, PackageStatus.Available);

                _logger.LogInformation("Deleting from the source for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId);

                await _packageFileService.DeleteValidationPackageFileAsync(validationSet);
            }

            // 5) Verify the package still exists (we've had bugs here before).
            if (validatingEntity.Status == PackageStatus.Available
                && !await _packageFileService.DoesPackageFileExistAsync(validationSet))
            {
                var validationPackageAvailable = await _packageFileService.DoesValidationPackageFileExistAsync(validationSet);

                _logger.LogWarning("Package {PackageId} {PackageVersion} is marked as available, but does not exist " +
                    "in public container. Does package exist in validation container: {ExistsInValidation}",
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationPackageAvailable);

                // Report missing package, don't try to fix up anything. This shouldn't happen and needs an investigation.
                _telemetryService.TrackMissingNupkgForAvailablePackage(
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId.ToString());
            }
        }

        /// <summary>
        /// Allows descendants to do additional operations before database is updated to mark package as available.
        /// </summary>
        /// <param name="validatingEntity">Entity being marked as available.</param>
        /// <param name="validationSet">Validation set that was completed.</param>
        protected virtual Task OnBeforeUpdateDatabaseToMakePackageAvailable(IValidatingEntity<T> validatingEntity, PackageValidationSet validationSet)
        {
            return Task.CompletedTask;
        }

        private async Task<PackageStatus> MarkPackageAsAvailableAsync(
            PackageValidationSet validationSet,
            IValidatingEntity<T> validatingEntity,
            PackageStreamMetadata streamMetadata,
            bool copied)
        {
            // Use whatever package made it into the packages container. This is what customers will consume so the DB
            // record must match.

            // We don't immediately commit here. Later, we will commit these changes as well as the new package
            // status as part of the same transaction.
            await _galleryPackageService.UpdateMetadataAsync(
                    validatingEntity.EntityRecord,
                    streamMetadata,
                    commitChanges: false);

            _logger.LogInformation("Marking package {PackageId} {PackageVersion}, validation set {ValidationSetId} as {PackageStatus} in DB",
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validationSet.ValidationTrackingId,
                PackageStatus.Available);

            var fromStatus = validatingEntity.Status;

            try
            {
                // Make the package available and commit any other pending changes (e.g. updated hash).
                await _galleryPackageService.UpdateStatusAsync(validatingEntity.EntityRecord, PackageStatus.Available, commitChanges: true);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    Error.UpdatingPackageDbStatusFailed,
                    e,
                    "Failed to update package status in Gallery Db. Package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId);

                // If this execution was not the one to copy the package, then don't delete the package on failure.
                // This prevents a missing passing in the (unlikely) case where two actors attempt the DB update, one
                // succeeds and one fails. We don't want an available package record with nothing in the packages
                // container!
                if (copied && fromStatus != PackageStatus.Available)
                {
                    await _packageFileService.DeletePackageFileAsync(validationSet);
                    await OnCleanupAfterDatabaseUpdateFailure(validatingEntity, validationSet);
                }

                throw;
            }

            return fromStatus;
        }

        /// <summary>
        /// Allows descendants to do additional cleanup on failure to update DB when marking package as available.
        /// Only called if package was copied to public container before trying to update DB.
        /// </summary>
        /// <param name="validatingEntity">Entity being marked as available.</param>
        /// <param name="validationSet">Validation set that was completed.</param>
        protected virtual Task OnCleanupAfterDatabaseUpdateFailure(
            IValidatingEntity<T> validatingEntity,
            PackageValidationSet validationSet)
        {
            return Task.CompletedTask;
        }

        private async Task<UpdatePublicPackageResult> UpdatePublicPackageAsync(PackageValidationSet validationSet)
        {
            _logger.LogInformation("Copying .nupkg to public storage for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validationSet.ValidationTrackingId);

            // If the validation set contains any processors, we must use the copy of the package that is specific to
            // this validation set. We can't use the original validation package because it does not have any of the
            // changes that the processors made. If the validation set package does not exist for some reason and there
            // are processors in the validation set, this indicates a bug and an exception will be thrown by the copy
            // operation below. This will cause the validation queue message to eventually dead-letter at which point
            // the on-call person should investigate.
            bool copied;
            PackageStreamMetadata metaData;
            if (validationSet.PackageValidations.Any(x => _validatorProvider.IsProcessor(x.Type)) ||
                await _packageFileService.DoesValidationSetPackageExistAsync(validationSet))
            {
                IAccessCondition destAccessCondition;

                // The package etag will be null if this validation set is expecting the package to not yet exist in
                // the packages container.
                if (validationSet.PackageETag == null)
                {
                    // This will fail with HTTP 409 if the package already exists. This means that another validation
                    // set has completed and moved the package into the Available state first, with different package
                    // content.
                    destAccessCondition = AccessConditionWrapper.GenerateIfNotExistsCondition();

                    _logger.LogInformation(
                        "Attempting to copy validation set {ValidationSetId} package {PackageId} {PackageVersion} to" +
                        " the packages container, assuming that the package does not already exist.",
                        validationSet.ValidationTrackingId,
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion);
                }
                else
                {
                    // This will fail with HTTP 412 if the package has been modified by another validation set. This
                    // would only happen if this validation set and another validation set are operating on a package
                    // already in the Available state.
                    destAccessCondition = AccessConditionWrapper.GenerateIfMatchCondition(validationSet.PackageETag);

                    _logger.LogInformation(
                        "Attempting to copy validation set {ValidationSetId} package {PackageId} {PackageVersion} to" +
                        " the packages container, assuming that the package has etag {PackageETag}.",
                        validationSet.ValidationTrackingId,
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validationSet.PackageETag);
                }

                metaData = await _packageFileService.UpdatePackageBlobMetadataInValidationSetAsync(validationSet);

                _logger.LogInformation(
                    "Updated the blob metadata of validation set {ValidationSetId} package {PackageId} {PackageVersion}",
                    validationSet.ValidationTrackingId,
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion);

                // Failures here should result in an unhandled exception. This means that this validation set has
                // modified the package but is unable to copy the modified package into the packages container because
                // another validation set completed first.
                await _packageFileService.CopyValidationSetPackageToPackageFileAsync(
                    validationSet,
                    destAccessCondition);

                copied = true;
            }
            else
            {
                _logger.LogInformation(
                    "The package specific to the validation set does not exist. Falling back to the validation " +
                    "container for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId);

                metaData = await _packageFileService.UpdatePackageBlobMetadataInValidationAsync(validationSet);

                _logger.LogInformation(
                    "Updated the blob metadata of validation {ValidationSetId} package {PackageId} {PackageVersion}",
                    validationSet.ValidationTrackingId,
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion);

                try
                {
                    await _packageFileService.CopyValidationPackageToPackageFileAsync(validationSet);

                    copied = true;
                }
                catch (InvalidOperationException)
                {
                    // The package already exists in the packages container. This can happen if the DB commit below fails
                    // and this flow is retried or another validation set for the package completed first. Either way, we
                    // will later attempt to use the hash from the package in the packages container (the destination).
                    // In other words, we don't care which copy wins when copying from the validation package because
                    // we know the package has not been modified.
                    _logger.LogInformation(
                        "Package already exists in packages container for {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion,
                        validationSet.ValidationTrackingId);

                    copied = false;
                }
            }

            return new UpdatePublicPackageResult(metaData, copied);
        }

        private class UpdatePublicPackageResult
        {
            public PackageStreamMetadata PackageStreamMetadata { get; }

            public bool Copied { get; }

            public UpdatePublicPackageResult(PackageStreamMetadata packageStreamMetadata, bool copied)
            {
                PackageStreamMetadata = packageStreamMetadata;
                Copied = copied;
            }
        }
    }
}
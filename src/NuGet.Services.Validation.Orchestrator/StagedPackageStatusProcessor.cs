// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class StagedPackageStatusProcessor : IStatusProcessor<StagedPackage>
    {
        private static readonly TimeSpan StagingCopyAccessDuration = TimeSpan.FromMinutes(10);

        private readonly IEntityService<StagedPackage> _entityService;

        private readonly IValidationFileService _packageFileService;

        private readonly IStagingBlobService _stagingBlobService;

        public StagedPackageStatusProcessor(
            IEntityService<StagedPackage> entityService,
            IValidationFileService packageFileService,
            IStagingBlobService stagingBlobService)
        {
            _entityService = entityService ?? throw new ArgumentNullException(nameof(entityService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
        }

        public Task SetStatusAsync(
            IValidatingEntity<StagedPackage> validatingEntity,
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

            switch (status)
            {
                case PackageStatus.Available:
                    return MarkReadyAsync(validatingEntity, validationSet);
                case PackageStatus.FailedValidation:
                    return MarkFailedValidationAsync(validatingEntity, validationSet);
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private async Task MarkReadyAsync(IValidatingEntity<StagedPackage> validatingEntity, PackageValidationSet validationSet)
        {
            if (!IsCurrent(validatingEntity, validationSet))
            {
                return;
            }

            var packageMetadata = await _packageFileService.UpdatePackageBlobMetadataInValidationSetAsync(validationSet);
            var packageFileUri = await _packageFileService.GetPackageForValidationSetReadUriAsync(
                validationSet,
                sasDefinition: null,
                DateTimeOffset.UtcNow.Add(StagingCopyAccessDuration));
            var file = await _stagingBlobService.CopyPackageFileToStagingAsync(
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                packageFileUri);

            await _entityService.UpdateMetadataAsync(validatingEntity.EntityRecord, packageMetadata, commitChanges: false);
            validatingEntity.EntityRecord.ValidatedBlobPath = file.Path;
            validatingEntity.EntityRecord.ValidatedBlobETag = file.ETag;
            await _entityService.UpdateStatusAsync(validatingEntity.EntityRecord, PackageStatus.Available, commitChanges: true);
        }

        private async Task MarkFailedValidationAsync(IValidatingEntity<StagedPackage> validatingEntity, PackageValidationSet validationSet)
        {
            if (!IsCurrent(validatingEntity, validationSet))
            {
                return;
            }

            await _entityService.UpdateStatusAsync(validatingEntity.EntityRecord, PackageStatus.FailedValidation, commitChanges: true);
        }

        private static bool IsCurrent(IValidatingEntity<StagedPackage> validatingEntity, PackageValidationSet validationSet)
        {
            return
                validatingEntity.Key == validationSet.PackageKey &&
                validatingEntity.EntityRecord.UploadedBlobETag == validationSet.PackageETag &&
                validatingEntity.EntityRecord.Status == StagedPackageStatus.Validating;
        }
    }
}

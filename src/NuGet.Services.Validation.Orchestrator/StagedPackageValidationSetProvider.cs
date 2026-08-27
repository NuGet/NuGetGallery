// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class StagedPackageValidationSetProvider : ValidationSetProvider<StagedPackage>
    {
        private readonly IValidationFileService _packageFileService;
        private readonly IStagingBlobService _stagingBlobService;

        public StagedPackageValidationSetProvider(
            IValidationStorageService validationStorageService,
            IValidationFileService packageFileService,
            IStagingBlobService stagingBlobService,
            IValidatorProvider validatorProvider,
            IOptionsSnapshot<ValidationConfiguration> validationConfigurationAccessor,
            IOptionsSnapshot<SasDefinitionConfiguration> sasDefinitionConfigurationAccessor,
            ITelemetryService telemetryService,
            ILogger<ValidationSetProvider<StagedPackage>> logger)
            : base(
                  validationStorageService,
                  packageFileService,
                  validatorProvider,
                  validationConfigurationAccessor,
                  sasDefinitionConfigurationAccessor,
                  telemetryService,
                  logger)
        {
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
        }

        protected override async Task CopyPackageFileToValidationSetAsync(PackageValidationSet validationSet, IValidatingEntity<StagedPackage> validatingEntity)
        {
            var stagedPackage = validatingEntity.EntityRecord;
            var packageUri = await _stagingBlobService.GetPackageReadUriAsync(stagedPackage.BlobPath, stagedPackage.BlobETag);

            await _packageFileService.CopyPackageUrlForValidationSetAsync(validationSet, packageUri.AbsoluteUri);

            validationSet.PackageETag = stagedPackage.BlobETag;
        }
    }
}

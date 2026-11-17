// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ImmediateStagedPackageValidationMessageEmitter : IStagedPackageValidationMessageEmitter
    {
        private readonly IStagingBlobService _stagingBlobService;

        public ImmediateStagedPackageValidationMessageEmitter(IStagingBlobService stagingBlobService)
        {
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
        }

        public async Task<StagedPackageStatus> StartValidationAsync(StagedPackage stagedPackage)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            var package = stagedPackage.StagedPackageIdentity.Package;
            StagingFileReference file;
            using (var stream = await _stagingBlobService.OpenPackageFileAsync(stagedPackage.UploadedBlobPath, stagedPackage.UploadedBlobETag))
            {
                file = await _stagingBlobService.SaveValidatedPackageFileAsync(package.PackageRegistration.Id, package.NormalizedVersion, stream);
            }

            stagedPackage.ValidatedBlobPath = file.Path;
            stagedPackage.ValidatedBlobETag = file.ETag;
            return StagedPackageStatus.Ready;
        }
    }
}

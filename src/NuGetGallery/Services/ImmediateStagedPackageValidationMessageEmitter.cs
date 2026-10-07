// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ImmediateStagedPackageValidationMessageEmitter : IStagedPackageValidationMessageEmitter
    {
        public Task<StagedPackageStatus> StartValidationAsync(StagedPackage stagedPackage)
        {
            if (stagedPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedPackage));
            }

            stagedPackage.ValidatedBlobPath = stagedPackage.UploadedBlobPath;
            stagedPackage.ValidatedBlobETag = stagedPackage.UploadedBlobETag;
            return Task.FromResult(StagedPackageStatus.Ready);
        }
    }
}

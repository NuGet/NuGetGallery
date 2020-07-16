// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public interface IPackageMetadataValidationService
    {
        Task<PackageValidationResult> ValidateMetadataBeforeUploadAsyn(
            PackageArchiveReader nuGetPackage,
            PackageMetadata packageMetadata,
            User currentUser);
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Gallery;
using NuGet.Services.Gallery.Entities;

namespace NuGetGallery
{
    public interface IAutomaticPackageCurator
    {
        Task CurateAsync(Package galleryPackage, PackageArchiveReader nugetPackage, bool commitChanges);
    }
}
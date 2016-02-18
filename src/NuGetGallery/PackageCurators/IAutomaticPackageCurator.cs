// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Packaging;

namespace NuGetGallery
{
    public interface IAutomaticPackageCurator
    {
        Task CurateAsync(Package galleryPackage, PackageArchiveReader nugetPackage, bool commitChanges);
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Packaging;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public interface IPackageUploadService
    {
        Task<Package> GeneratePackageAsync(string id, PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User user, bool commitChanges = true);
    }
}
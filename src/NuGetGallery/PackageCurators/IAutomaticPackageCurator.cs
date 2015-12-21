// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging;

namespace NuGetGallery
{
    public interface IAutomaticPackageCurator
    {
        void Curate(Package galleryPackage, PackageReader nugetPackage, bool commitChanges);
    }
}
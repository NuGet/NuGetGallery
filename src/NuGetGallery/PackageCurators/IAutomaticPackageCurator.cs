// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public interface IAutomaticPackageCurator
    {
        void Curate(Package galleryPackage, INupkg nugetPackage, bool commitChanges);
    }
}
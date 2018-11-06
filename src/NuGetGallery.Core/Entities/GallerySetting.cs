// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    // These guys are no longer referenced by code, but they are still referenced by
    // UpdateLicenseReportsTask in NuGet.Gallery.Operations, so need to be part of the data model.
    public class GallerySetting : IEntity
    {
        public int Key { get; set; }
        public string NextLicenseReport { get; set; }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Packaging
{
    public static class PackagingConstants
    {
        /// <summary>
        /// The maximum package ID length enforced by the database.
        /// This is not equal to the maximum package length that can be submitted to the gallery.
        /// See <see cref="NuGet.Packaging.PackageIdValidator.MaxPackageIdLength"/> for the maximum length that is enforced by the gallery.
        /// </summary>
        public const int PackageIdDatabaseLength = 128;
    }
}

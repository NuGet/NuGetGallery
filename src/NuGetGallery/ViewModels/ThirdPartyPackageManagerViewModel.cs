// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// A Package Manager that conforms to the NuGet protocol but isn't
    /// maintained by the NuGet team.
    /// </summary>
    public class ThirdPartyPackageManagerViewModel : PackageManagerViewModel
    {
        /// <summary>
        /// The URL that links should be used to contact the package manager's
        /// maintainers for support.
        /// </summary>
        public string ContactUrl { get; set; }
    }
}
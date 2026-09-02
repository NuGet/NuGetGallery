// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Describes an update to a staged package.
    /// </summary>
    public class UpdateStagedPackageRequest
    {
        /// <summary>
        /// Gets or sets whether the package should be listed after promotion.
        /// </summary>
        public bool Listed { get; set; }
    }
}

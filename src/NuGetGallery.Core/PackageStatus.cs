// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public enum PackageStatus
    {
        /// <summary>
        /// The package is visible and available for all normal package operations.
        /// </summary>
        Available = 0,

        /// <summary>
        /// The package has been soft deleted. This means that the package is not available but the package ID and
        /// version are still reserved.
        /// </summary>
        Deleted = 1,
    }
}

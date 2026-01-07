// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibilityData
    {
        /// <summary>
        /// Compatible framework.
        /// </summary>
        public NuGetFramework Framework { get; set; }

        /// <summary>
        /// <see langword="true"/> if the <see cref="Framework"/> was computed from <see cref="FrameworkCompatibilityService"/>.<br></br>
        /// <see langword="false"/> if the <see cref="Framework"/> was retrieved from the package asset frameworks.
        /// </summary>
        public bool IsComputed { get; set; }
    }
}

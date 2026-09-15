// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    /// <summary>
    /// Represents the framework shown in a <see cref="PackageFrameworkCompatibilityBadges"/> badge.
    /// </summary>
    public class PackageFrameworkCompatibilityBadgeData : PackageFrameworkCompatibilityData
    {
        /// <summary>
        /// The earliest (lowest version) framework in the same .NET product that matches the badge's inclusion
        /// criteria (i.e. <see cref="PackageFrameworkCompatibilityData.IsComputed"/>). <br></br>
        /// Only populated when it differs from <see cref="PackageFrameworkCompatibilityData.Framework"/>, indicating
        /// that the package supports a range of framework versions rather than a single one. Used to let consumers
        /// know the package remains backwards compatible even though the badge highlights the latest supported version.
        /// </summary>
        public NuGetFramework EarliestFramework { get; set; }
    }
}

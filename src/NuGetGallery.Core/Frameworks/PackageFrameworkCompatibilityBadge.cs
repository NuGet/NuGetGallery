// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibilityBadge
    {
        /// <summary>
        /// .NET based framework product name of the <see cref="Framework"/> from the list of <see cref="FrameworkProductNames"/>.
        /// </summary>
        public string FrameworkProductName { get; set; }

        /// <summary>
        /// .NET based package asset framework that is going to be displayed as badge.
        /// </summary>
        public NuGetFramework Framework { get; set; }

        /// <summary>
        /// True if the package contains more asset frameworks with the same <see cref="FrameworkProductName"/> that are higher versions than <see cref="Framework"/>.
        /// </summary>
        public bool HasHigherVersions { get; set; }
    }
}

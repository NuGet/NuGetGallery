// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    [Flags]
    public enum SearchFilters
    {
        /// <summary>
        /// Exclude SemVer 2.0.0 and prerelease packages.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Include packages that have prerelease versions. Note that a package's dependency version ranges do not
        /// affect the prerelease status of the package. This is in contrast of <see cref="IncludeSemVer2"/>.
        /// </summary>
        IncludePrerelease = 1 << 0,

        /// <summary>
        /// Include SemVer 2.0.0 packages. Note that SemVer 2.0.0 dependency version ranges make a package into a SemVer
        /// 2.0.0 even if the package's own version string is SemVer 1.0.0.
        /// </summary>
        IncludeSemVer2 = 1 << 1,

        /// <summary>
        /// Include package that have prerelease versions and include SemVer 2.0.0 packages.
        /// </summary>
        IncludePrereleaseAndSemVer2 = IncludePrerelease | IncludeSemVer2,
    }
}

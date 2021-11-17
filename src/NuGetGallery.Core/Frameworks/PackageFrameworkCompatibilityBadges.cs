// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    /// <summary>
    /// Contains a <see cref="NuGetFramework"/> for each of the .NET product (.NET, .NET Core, .NET Standard, and .NET Framework).
    /// </summary>
    /// <remarks>
    /// All these properties are retrieved from the <see cref="PackageFrameworkCompatibility.Table"/>, one for each .NET product.<br></br>
    /// Only package asset frameworks are considered. i.e. <see cref="PackageFrameworkCompatibilityTableData.IsComputed"/> <c>= false</c>
    /// </remarks>
    public class PackageFrameworkCompatibilityBadges
    {
        public NuGetFramework Net { get; set; }
        public NuGetFramework NetCore { get; set; }
        public NuGetFramework NetStandard { get; set; }
        public NuGetFramework NetFramework { get; set; }
    }
}

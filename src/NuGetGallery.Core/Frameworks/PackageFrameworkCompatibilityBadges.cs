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
    /// Only package asset frameworks are considered. i.e. <see cref="PackageFrameworkCompatibilityData.IsComputed"/> <c>= false</c>.<br></br>
    /// If there are no package asset frameworks for a particular .NET product, then the value will be <c>null</c>.
    /// </remarks>
    public class PackageFrameworkCompatibilityBadges
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public PackageFrameworkCompatibilityData Net { get; set; }
        public PackageFrameworkCompatibilityData NetCore { get; set; }
        public PackageFrameworkCompatibilityData NetStandard { get; set; }
        public PackageFrameworkCompatibilityData NetFramework { get; set; }
    }
}

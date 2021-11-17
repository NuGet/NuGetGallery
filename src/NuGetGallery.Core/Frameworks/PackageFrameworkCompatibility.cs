// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using System.Collections.Generic;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibility
    {
        /// <summary>
        /// Contains a <see cref="NuGetFramework"/> for each of the .NET framework products (.NET, .NET Core, .NET Standard, and .NET Framework).
        /// </summary>
        public PackageFrameworkCompatibilityBadges Badges { get; set; }

        /// <summary>
        /// Contains a dictionary filled with all the package asset frameworks and all the computed compatible frameworks retrieved from <see cref="FrameworkCompatibilityService"/>.<br></br>
        /// </summary>
        /// <remarks>
        /// Key: Is the <see cref="FrameworkProductNames"/> if resolved on (<seealso cref="PackageFrameworkCompatibilityFactory.ResolveFrameworkProductName(NuGetFramework)"/>) or the <see cref="NuGetFramework.Framework"/>.<br></br>
        /// Value: Is an ordered collection containing all the compatible frameworks.
        /// </remarks>
        public IReadOnlyDictionary<string, ICollection<PackageFrameworkCompatibilityTableData>> Table { get; set; }
    }
}

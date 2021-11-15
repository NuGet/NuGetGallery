// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using System.Collections.Generic;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibility
    {
        public PackageFrameworkCompatibilityBadges Badges { get; set; }
        public IReadOnlyDictionary<string, ISet<PackageFrameworkCompatibilityTableData>> Table { get; set; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibilityBadges
    {
        public NuGetFramework Net { get; set; }
        public NuGetFramework NetCore { get; set; }
        public NuGetFramework NetStandard { get; set; }
        public NuGetFramework NetFramework { get; set; }
    }
}

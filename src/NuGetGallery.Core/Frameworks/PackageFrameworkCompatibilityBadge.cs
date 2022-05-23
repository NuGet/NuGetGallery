// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibilityBadge
    {
        public string FrameworkProduct { get; set; }
        public NuGetFramework Framework { get; set; }
        public bool IsHighestVersion { get; set; }
    }
}

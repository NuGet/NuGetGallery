// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibilityTableData
    {
        public NuGetFramework Framework { get; set; }
        public bool IsComputed { get; set; }
    }
}

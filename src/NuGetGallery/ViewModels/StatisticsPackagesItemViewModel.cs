// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class StatisticsPackagesItemViewModel
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string PackageTitle { get; set; }
        public string PackageDescription { get; set; }
        public string PackageIconUrl { get; set; }
        public int Downloads { get; set; }
    }
}
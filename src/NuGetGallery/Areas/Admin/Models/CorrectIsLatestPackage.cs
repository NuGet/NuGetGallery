// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.Models
{
    public class CorrectIsLatestPackage
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public int IsLatestCount { get; set; }
        public int IsLatestStableCount { get; set; }
        public int IsLatestSemVer2Count { get; set; }
        public int IsLatestStableSemVer2Count { get; set; }
        public bool HasIsLatestUnlisted { get; set; }
    }
}
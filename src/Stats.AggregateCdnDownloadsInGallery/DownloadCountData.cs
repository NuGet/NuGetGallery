// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.AggregateCdnDownloadsInGallery
{
    public class DownloadCountData
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public long TotalDownloadCount { get; set; }
    }
}
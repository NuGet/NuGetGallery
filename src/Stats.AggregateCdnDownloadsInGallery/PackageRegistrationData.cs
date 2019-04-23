// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.AggregateCdnDownloadsInGallery
{
    public class PackageRegistrationData
    {
        public string Key { get; set; }
        public string LowercasedId { get; set; }
        public string OriginalId { get; set; }
        public string DownloadCount { get; set; }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.AggregateCdnDownloadsInGallery
{
    public class AggregateCdnDownloadsConfiguration
    {
        public int BatchSize { get; set; }

        public int BatchSleepSeconds { get; set; }

        public int? CommandTimeoutSeconds { get; set; }
    }
}

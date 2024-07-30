// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Stats.AggregateCdnDownloadsInGallery
{
    public static class LogEvents
    {
        public static EventId IncorrectIdsInGroupBatch = new EventId(900, "The group batch to be ingested contains multiple package Ids.");
        public static EventId DownloadCountDecreaseDetected = new EventId(901, "The download count for the package is decreasing.");

    }
}

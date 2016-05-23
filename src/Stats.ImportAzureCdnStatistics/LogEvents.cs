// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Stats.ImportAzureCdnStatistics
{
    internal class LogEvents
    {
        internal static EventId FailedToProcessLogFile = new EventId(500, "Failed to process log file");
        internal static EventId FailedToParseLogFile = new EventId(501, "Failed to parse log file");
        internal static EventId FailedToDecompressBlob = new EventId(502, "Failed to decompress blob");
        internal static EventId FailedBlobUpload = new EventId(503, "Failed to upload blob");
        internal static EventId FailedBlobDelete = new EventId(504, "Failed to delete blob");
        internal static EventId FailedBlobListing = new EventId(505, "Failed to list blobs");
        internal static EventId FailedBlobLease = new EventId(506, "Failed to lease blob");
        internal static EventId FailedDimensionRetrieval = new EventId(507, "Failed to retrieve dimension");
    }
}
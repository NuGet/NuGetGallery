// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Stats.AzureCdnLogs.Common
{
    public class LogEvents
    {
        public static EventId FailedToProcessLogFile = new EventId(500, "Failed to process log file");
        public static EventId FailedToParseLogFile = new EventId(501, "Failed to parse log file");
        public static EventId FailedToDecompressBlob = new EventId(502, "Failed to decompress blob");
        public static EventId FailedBlobUpload = new EventId(503, "Failed to upload blob");
        public static EventId FailedBlobDelete = new EventId(504, "Failed to delete blob");
        public static EventId FailedBlobListing = new EventId(505, "Failed to list blobs");
        public static EventId FailedBlobLease = new EventId(506, "Failed to lease blob");
        public static EventId FailedDimensionRetrieval = new EventId(507, "Failed to retrieve dimension");
        public static EventId FailedToParseLogFileEntry = new EventId(508, "Failed to parse log file entry");
    }
}
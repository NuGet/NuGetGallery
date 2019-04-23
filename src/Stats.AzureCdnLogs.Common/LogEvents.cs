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
        public static EventId FailedToProcessLogStream = new EventId(509, "Error processing log stream");
        public static EventId UnknownAzureCdnPlatform = new EventId(510, "Unknown Azure CDN platform");
        public static EventId InvalidRawLogFileName = new EventId(511, "Invalid raw log filename");
        public static EventId FailedToGetFtpResponse = new EventId(512, "Failed to get FTP response");
        public static EventId FailedToCheckAlreadyProcessedLogFilePackageStatistics = new EventId(513, "Failed to check already processed package statistics for log file");
        public static EventId FailedToCheckAlreadyProcessedLogFileToolStatistics = new EventId(514, "Failed to check already processed tool statistics for log file");
        public static EventId MultiplePackageIDVersionParseOptions = new EventId(515, "Multiple package id/version parse options");
        public static EventId TranslatedPackageIdVersion = new EventId(516, "Translated package id and version");
        public static EventId FailedBlobCopy = new EventId(517, "Failed to copy blob.");
        public static EventId FailedBlobReleaseLease = new EventId(518, "Failed to release lease for blob.");
        public static EventId JobRunFailed = new EventId(550, "Job run failed");
        public static EventId JobInitFailed = new EventId(551, "Job initialization failed");       
        public static EventId FailedToProcessStream = new EventId(560, "Failed to process the stream.");
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class StatisticsReportResult
    {
        public static readonly StatisticsReportResult Failed = new StatisticsReportResult(
            isLoaded: false,
            lastUpdatedUtc: null);

        public bool IsLoaded { get; private set; }

        public DateTime? LastUpdatedUtc { get; private set; }

        private StatisticsReportResult(bool isLoaded, DateTime? lastUpdatedUtc)
        {
            IsLoaded = isLoaded;
            LastUpdatedUtc = lastUpdatedUtc;
        }

        public static StatisticsReportResult Success(DateTimeOffset? lastUpdated)
        {
            return Success(lastUpdated?.UtcDateTime);
        }

        public static StatisticsReportResult Success(DateTime? lastUpdatedUtc)
        {
            return new StatisticsReportResult(isLoaded: true, lastUpdatedUtc: lastUpdatedUtc);
        }
    }
}

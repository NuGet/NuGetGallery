// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace NuGetGallery
{
    public class StatisticsReportResult
    {
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "This object is immutable")]
        public static readonly StatisticsReportResult Failed = new StatisticsReportResult(
            loaded: false, 
            lastUpdatedUtc: null);

        public bool Loaded { get; private set; }
        public DateTime? LastUpdatedUtc { get; private set; }

        private StatisticsReportResult(bool loaded, DateTime? lastUpdatedUtc)
        {
            Loaded = loaded;
            LastUpdatedUtc = lastUpdatedUtc;
        }

        public static StatisticsReportResult Success(DateTimeOffset? lastUpdated)
        {
            return Success(lastUpdated.HasValue ? lastUpdated.Value.UtcDateTime : (DateTime?)null);
        }

        public static StatisticsReportResult Success(DateTime? lastUpdatedUtc)
        {
            return new StatisticsReportResult(loaded: true, lastUpdatedUtc: lastUpdatedUtc);
        }
    }
}

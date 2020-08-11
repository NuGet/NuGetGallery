// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class IndexStatus
    {
        public string Name { get; set; }
        public long DocumentCount { get; set; }
        public TimeSpan DocumentCountDuration { get; set; }
        public TimeSpan WarmQueryDuration { get; set; }
        public DateTimeOffset? LastCommitTimestamp { get; set; }
        public TimeSpan LastCommitTimestampDuration { get; set; }
    }
}

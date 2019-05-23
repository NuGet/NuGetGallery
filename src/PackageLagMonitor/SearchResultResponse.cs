// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class SearchResultResponse
    {
        public long TotalHits { get; set; }
        public string Index { get; set; }
        public DateTimeOffset IndexTimeStamp { get; set; }
        public SearchResult[] Data { get; set; }
    }

    public class SearchResult
    {
        public DateTimeOffset Created { get; set; }

        public DateTimeOffset LastEdited { get; set; }

        public bool Listed { get; set; }
    }
}

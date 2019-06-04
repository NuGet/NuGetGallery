// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class SearchDiagnosticResponse
    {
        public long NumDocs { get; set; }
        public string IndexName { get; set; }
        public long LastIndexReloadDurationInMilliseconds { get; set; }
        public DateTimeOffset LastIndexReloadTime { get; set; }
        public DateTimeOffset LastReopen { get; set; }
        public CommitUserData CommitUserData { get; set; }
    }

    public class CommitUserData
    {
        public string CommitTimeStamp { get; set; }
        public string Description { get; set; }
        public string Count { get; set; }
        public string Trace { get; set; }
    }
}

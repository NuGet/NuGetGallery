// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class SearchDiagnosticResponse
    {
        public DateTimeOffset LastIndexReloadTime { get; set; }
        public CommitUserData CommitUserData { get; set; }
    }

    public class CommitUserData
    {
        public string CommitTimeStamp { get; set; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class AzureSearchDiagnosticResponse
    {
        public IndexInformation SearchIndex { get; set; }
    }

    public class IndexInformation
    {
        public string Name { get; set; }
        
        public long DocumentCount { get; set; }

        public DateTimeOffset LastCommitTimestamp { get; set; }

        public TimeSpan LastCommitTimestampDuration { get; set; }
     }
}

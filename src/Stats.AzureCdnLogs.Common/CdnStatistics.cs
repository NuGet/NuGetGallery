// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Stats.AzureCdnLogs.Common
{
    public class CdnStatistics
    {
        public CdnStatistics(IReadOnlyCollection<PackageStatistics> packageStatistics, IReadOnlyCollection<ToolStatistics> toolStatistics)
        {
            PackageStatistics = packageStatistics;
            ToolStatistics = toolStatistics;
        }

        public IReadOnlyCollection<PackageStatistics> PackageStatistics { get; set; }
        public IReadOnlyCollection<ToolStatistics> ToolStatistics { get; set; }
    }
}
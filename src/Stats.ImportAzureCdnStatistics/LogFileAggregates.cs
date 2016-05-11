// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Stats.ImportAzureCdnStatistics
{
    internal class LogFileAggregates
    {
        public LogFileAggregates(string logFileName)
        {
            LogFileName = logFileName;
            PackageDownloadsByDate = new Dictionary<int, int>();
        }

        public string LogFileName { get; private set; }

        public IDictionary<int, int> PackageDownloadsByDate { get; private set; }
    }
}
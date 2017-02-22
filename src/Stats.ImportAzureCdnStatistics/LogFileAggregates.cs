// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Stats.ImportAzureCdnStatistics
{
    public class LogFileAggregates
    {
        public LogFileAggregates(string logFileName)
        {
            LogFileName = logFileName;
            PackageDownloadsByDateDimensionId = new Dictionary<int, int>();
        }

        public string LogFileName { get; private set; }

        /// <summary>
        /// Contains date dimension id's linked to total package download counts for a given log file.
        /// </summary>
        public IDictionary<int, int> PackageDownloadsByDateDimensionId { get; private set; }
    }
}
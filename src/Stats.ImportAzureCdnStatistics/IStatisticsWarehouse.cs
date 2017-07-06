// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public interface IStatisticsWarehouse
    {
        Task<DataTable> CreateAsync(IReadOnlyCollection<PackageStatistics> sourceData, string logFileName);
        Task<DataTable> CreateAsync(IReadOnlyCollection<ToolStatistics> sourceData, string logFileName);
        Task<bool> HasImportedToolStatisticsAsync(string logFileName);
        Task<bool> HasImportedPackageStatisticsAsync(string logFileName);
        Task InsertDownloadFactsAsync(DataTable downloadFactsDataTable, string logFileName);
        Task StoreLogFileAggregatesAsync(LogFileAggregates logFileAggregates);
    }
}
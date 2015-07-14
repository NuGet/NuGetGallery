// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;

namespace Stats.AzureCdnLogs.Common
{
    public class AggregatePackageStatisticsTable
        : AzureTableBase<PackageDownloadStatistic>
    {
        public AggregatePackageStatisticsTable(CloudStorageAccount cloudStorageAccount)
            : base(cloudStorageAccount, typeof (PackageStatistics).Name + "aggregated")
        {
        }
    }
}
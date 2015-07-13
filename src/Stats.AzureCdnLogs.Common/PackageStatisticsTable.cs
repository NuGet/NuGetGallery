// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace Stats.AzureCdnLogs.Common
{
    public class PackageStatisticsTable
        : AzureTableBase<PackageStatistics>
    {

        public PackageStatisticsTable(CloudStorageAccount cloudStorageAccount)
            : base(cloudStorageAccount)
        {
        }

        public override async Task<bool> CreateIfNotExistsAsync()
        {
            return await base.CreateIfNotExistsAsync();
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;

namespace Stats.AzureCdnLogs.Common
{
    public class TotalPackageDownloadStatistic
        : TableEntity
    {
        public string PackageId
        {
            get { return PartitionKey; }
            set { PartitionKey = value; }
        }

        public string PackageVersion
        {
            get { return RowKey; }
            set { RowKey = value; }
        }

        public int TotalDownloadCount { get; set; }
    }
}
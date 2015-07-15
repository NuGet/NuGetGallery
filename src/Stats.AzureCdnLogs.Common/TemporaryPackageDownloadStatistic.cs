// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;

namespace Stats.AzureCdnLogs.Common
{
    public class TemporaryPackageDownloadStatistic
        : TableEntity
    {
        private string _packageVersion;
        private string _aggregatorId;

        public string PackageId
        {
            get { return PartitionKey; }
            set { PartitionKey = value; }
        }

        public string PackageVersion
        {
            get { return _packageVersion; }
            set
            {
                _packageVersion = value;
                PopulateRowKey();
            }
        }

        public string AggregatorId
        {
            get { return _aggregatorId; }
            set
            {
                _aggregatorId = value;
                PopulateRowKey();
            }
        }

        public int TotalDownloadCount { get; set; }

        private void PopulateRowKey()
        {
            RowKey = GenerateRowKey(_aggregatorId, _packageVersion);
        }

        public static string GenerateRowKey(string id, string version)
        {
            return id + "__" + version;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.ImportAzureCdnStatistics
{
    public class ImportAzureCdnStatisticsConfiguration
    {
        public string AzureCdnCloudStorageAccount { get; set; }

        public string AzureCdnCloudStorageContainerName { get; set; }

        public string AzureCdnPlatform { get; set; }

        public string AzureCdnAccountNumber { get; set; }

        public bool AggregatesOnly { get; set; }
    }
}

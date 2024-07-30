// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.CollectAzureCdnLogs
{
    public class CollectAzureCdnLogsConfiguration
    {
        public string AzureCdnAccountNumber { get; set; }

        public string AzureCdnCloudStorageAccount { get; set; }

        public string AzureCdnCloudStorageContainerName { get; set; }

        public string AzureCdnPlatform { get; set; }

        public string FtpSourceUri { get; set; }

        public string FtpSourceUsername { get; set; }

        public string FtpSourcePassword { get; set; }
    }
}

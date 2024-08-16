// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.CollectAzureChinaCDNLogs
{
    public class CollectAzureChinaCdnLogsConfiguration
    {
        public string AzureAccountConnectionStringSource { get; set; }

        public string AzureAccountConnectionStringDestination { get; set; }

        public string AzureContainerNameSource { get; set; }

        public string AzureContainerNameDestination { get; set; }

        public string DestinationFilePrefix { get; set; }

        public int? ExecutionTimeoutInSeconds { get; set; }
    }
}

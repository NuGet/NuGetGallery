// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.CollectAzureChinaCDNLogs
{
    public class ArgumentNames
    {
        internal const string AzureAccountConnectionStringSource = "AzureAccountConnectionStringSource";
        internal const string AzureAccountConnectionStringDestination = "AzureAccountConnectionStringDestination";
        internal const string AzureContainerNameDestination = "AzureContainerNameDestination";
        internal const string AzureContainerNameSource = "AzureContainerNameSource";
        internal const string DestinationFilePrefix = "DestinationFilePrefix";
        //a timeout in seconds for an execution loop
        internal const string ExecutionTimeoutInSeconds = "ExecutionTimeoutInSeconds";
    }
}

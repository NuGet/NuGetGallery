// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.CDNLogsSanitizer
{
    public class JobConfiguration
    {
        public string AzureAccountConnectionStringSource { get; set; }

        public string AzureAccountConnectionStringDestination { get; set; }

        public string AzureContainerNameSource { get; set; }

        public string AzureContainerNameDestination { get; set; }

        public int? ExecutionTimeoutInSeconds { get; set; }

        public int? MaxBlobsToProcess { get; set; }

        public string LogHeader { get; set; }

        public char? LogHeaderDelimiter { get; set; }

        public string BlobPrefix { get; set; }
    }
}

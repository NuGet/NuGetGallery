// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.ParseAzureCdnLogs
{
    internal class CloudBlobRawLogSource
    {
        private readonly JobEventSource _jobEventSource;
        private readonly CloudBlobContainer _container;

        public CloudBlobRawLogSource(JobEventSource jobEventSource, CloudBlobContainer container)
        {
            _jobEventSource = jobEventSource;
            _container = container;
        }

        public async Task<IEnumerable<IListBlobItem>> ListNextLogFileToBeProcessedAsync(string prefix)
        {
            var blobResultSegment = await _container.ListBlobsSegmentedAsync(prefix, null);
            return blobResultSegment.Results;
        }
    }
}
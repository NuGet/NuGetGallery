// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.ImportAzureCdnStatistics
{
    public class CloudBlobRawLogSource
    {
        private readonly JobEventSource _jobEventSource;
        private readonly CloudBlobContainer _container;

        public CloudBlobRawLogSource(JobEventSource jobEventSource, CloudBlobContainer container)
        {
            _jobEventSource = jobEventSource;
            _container = container;
        }

        public async Task<IListBlobItem> ListNextLogFileToBeProcessedAsync(string prefix)
        {
            try
            {
                _jobEventSource.BeginningBlobListing(prefix);
                var blobResultSegment = await _container.ListBlobsSegmentedAsync(prefix, true, BlobListingDetails.None, 1, null, null, null);
                _jobEventSource.FinishingBlobListing(prefix);
                return blobResultSegment.Results.SingleOrDefault();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.ToString());
                _jobEventSource.FailedBlobListing(prefix);
                return null;
            }
        }
    }
}
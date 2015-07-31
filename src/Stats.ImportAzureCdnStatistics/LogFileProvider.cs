// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.ImportAzureCdnStatistics
{
    internal class LogFileProvider
    {
        private const int _maxListBlobResultSegments = 5;
        private readonly TimeSpan _defaultLeaseTime = TimeSpan.FromSeconds(60);
        private readonly JobEventSource _jobEventSource = JobEventSource.Log;
        private readonly CloudBlobContainer _container;

        public LogFileProvider(CloudBlobContainer container)
        {
            _container = container;
        }

        public async Task<ILeasedLogFile> LeaseNextLogFileToBeProcessedAsync(string prefix)
        {
            try
            {
                _jobEventSource.BeginningBlobListing(prefix);
                var blobResultSegments = await _container.ListBlobsSegmentedAsync(prefix, true, BlobListingDetails.None, _maxListBlobResultSegments, null, null, null);
                _jobEventSource.FinishingBlobListing(prefix);

                foreach (var logFile in blobResultSegments.Results)
                {
                    // Get the source blob
                    var blobName = logFile.Uri.Segments.Last();
                    var logFileBlob = _container.GetBlockBlobReference(blobName);

                    // try to acquire a lease on the blob
                    var leaseId = await TryAcquireLeaseAsync(logFileBlob);
                    if (string.IsNullOrEmpty(leaseId))
                    {
                        // the blob is already leased, ignore it and move on
                        continue;
                    }

                    return new LeasedLogFile(logFileBlob, leaseId);
                }

            }
            catch (Exception e)
            {
                Trace.TraceError(e.ToString());
                _jobEventSource.FailedBlobListing(prefix);
            }

            return null;
        }

        private async Task<string> TryAcquireLeaseAsync(ICloudBlob blob)
        {
            string leaseId;
            var blobUriString = blob.Uri.ToString();
            try
            {
                var sourceBlobExists = await blob.ExistsAsync();
                if (!sourceBlobExists)
                {
                    return null;
                }

                _jobEventSource.BeginningAcquireLease(blobUriString);
                leaseId = await blob.AcquireLeaseAsync(_defaultLeaseTime, null);
                _jobEventSource.FinishedAcquireLease(blobUriString);
            }
            catch (StorageException storageException)
            {
                // check if this is a 409 Conflict with a StatusDescription stating that "There is already a lease present."
                var webException = storageException.InnerException as WebException;
                if (webException != null)
                {
                    var httpWebResponse = webException.Response as HttpWebResponse;
                    if (httpWebResponse != null)
                    {
                        if (httpWebResponse.StatusCode == HttpStatusCode.Conflict
                            && httpWebResponse.StatusDescription == "There is already a lease present.")
                        {
                            return null;
                        }
                    }
                }
                _jobEventSource.FailedAcquireLease(blobUriString);
                throw;
            }
            return leaseId;
        }


        private class LeasedLogFile
            : ILeasedLogFile
        {
            private readonly JobEventSource _jobEventSource = JobEventSource.Log;
            private readonly Thread _autoRenewLeaseThread;
            private readonly TimeSpan _leaseExpirationThreshold = TimeSpan.FromSeconds(40);

            internal LeasedLogFile(CloudBlockBlob blob, string leaseId)
            {
                if (blob == null)
                    throw new ArgumentNullException(nameof(blob));
                if (leaseId == null)
                    throw new ArgumentNullException(nameof(leaseId));

                Blob = blob;
                LeaseId = leaseId;
                Uri = blob.Uri.ToString();

                // hold on to the lease for the duration of this method-action by auto-renewing in the background
                _autoRenewLeaseThread = StartNewAutoRenewLeaseThread(blob, leaseId);
            }

            public string Uri { get; private set; }

            public CloudBlockBlob Blob { get; private set; }

            public string LeaseId { get; private set; }

            public async Task AcquireInfiniteLeaseAsync()
            {
                _autoRenewLeaseThread.Abort();

                // ... by taking an infinite lease (as long as the lease is there, no other job instance will be able to acquire a lease on it and attempt processing it)
                await Blob.AcquireLeaseAsync(null, LeaseId);
            }

            private Thread StartNewAutoRenewLeaseThread(ICloudBlob blob, string leaseId)
            {
                var autoRenewLeaseThread = new Thread(
                    async () =>
                    {
                        while (await blob.ExistsAsync())
                        {
                            // auto-renew lease when about to expire
                            Thread.Sleep(_leaseExpirationThreshold);
                            var blobUriString = blob.Uri.ToString();
                            try
                            {
                                _jobEventSource.BeginningRenewLease(blobUriString);
                                await blob.RenewLeaseAsync(AccessCondition.GenerateLeaseCondition(leaseId));
                                _jobEventSource.FinishedRenewLease(blobUriString);
                            }
                            catch
                            {
                                // the blob could have been deleted in the meantime
                                // this thread will be killed either way
                                _jobEventSource.FailedRenewLease(blobUriString);
                            }
                        }
                    });
                autoRenewLeaseThread.Start();
                return autoRenewLeaseThread;
            }

            public void Dispose()
            {
                if (_autoRenewLeaseThread != null && _autoRenewLeaseThread.IsAlive)
                {
                    _autoRenewLeaseThread.Abort();
                }
            }
        }
    }
}
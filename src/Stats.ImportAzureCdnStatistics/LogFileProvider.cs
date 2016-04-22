// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.ImportAzureCdnStatistics
{
    internal class LogFileProvider
    {
        private const int _maxListBlobResultSegments = 30;
        private const int _maxLeasesPerJobRun = 3;
        private readonly TimeSpan _defaultLeaseTime = TimeSpan.FromSeconds(60);
        private readonly CloudBlobContainer _container;
        private readonly ILogger _logger;

        public LogFileProvider(CloudBlobContainer container, ILoggerFactory loggerFactory)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _container = container;
            _logger = loggerFactory.CreateLogger<LogFileProvider>();
        }

        public async Task<IReadOnlyCollection<ILeasedLogFile>> LeaseNextLogFilesToBeProcessedAsync(string prefix)
        {
            try
            {
                _logger.LogDebug("Beginning blob listing using prefix {BlobPrefix}.", prefix);

                var blobResultSegments = await _container.ListBlobsSegmentedAsync(prefix, true, BlobListingDetails.None, _maxListBlobResultSegments, null, null, null);

                _logger.LogInformation("Finishing blob listing using prefix {BlobPrefix}.", prefix);

                var leasedFiles = new List<ILeasedLogFile>();

                foreach (var logFile in blobResultSegments.Results)
                {
                    if (leasedFiles.Count == _maxLeasesPerJobRun)
                    {
                        break;
                    }

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

                    leasedFiles.Add(new LeasedLogFile(logFileBlob, leaseId));
                }

                return leasedFiles;
            }
            catch (Exception exception)
            {
                _logger.LogError(new FormattedLogValues("Failed blob listing using prefix {BlobPrefix}.", prefix), exception);
            }

            return Enumerable.Empty<ILeasedLogFile>().ToList();
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

                _logger.LogDebug("Beginning to acquire lease for blob {BlobUri}.", blobUriString);

                leaseId = await blob.AcquireLeaseAsync(_defaultLeaseTime);

                _logger.LogInformation("Finishing to acquire lease for blob {BlobUri}.", blobUriString);
            }
            catch (StorageException storageException)
            {
                // check if this is a 409 Conflict with a StatusDescription stating that "There is already a lease present."
                // or 404 NotFound (might have been removed by another other instance of this job)
                var webException = storageException.InnerException as WebException;
                if (webException != null)
                {
                    var httpWebResponse = webException.Response as HttpWebResponse;
                    if (httpWebResponse != null)
                    {
                        if ((httpWebResponse.StatusCode == HttpStatusCode.Conflict
                            && httpWebResponse.StatusDescription == "There is already a lease present.") || httpWebResponse.StatusCode == HttpStatusCode.NotFound)
                        {
                            _logger.LogDebug("Failed to acquire lease for blob {BlobUri}.", blobUriString); // no need to report these in Application Insights
                            return null;
                        }
                    }
                }

                _logger.LogError(new FormattedLogValues("Failed to acquire lease for blob {BlobUri}.", blobUriString), storageException);

                throw;
            }
            return leaseId;
        }


        private class LeasedLogFile
            : ILeasedLogFile
        {
            private readonly Thread _autoRenewLeaseThread;
            private readonly TimeSpan _leaseExpirationThreshold = TimeSpan.FromSeconds(40);
            private readonly CancellationTokenSource _cancellationTokenSource;

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
                _cancellationTokenSource = new CancellationTokenSource();
                _autoRenewLeaseThread = StartNewAutoRenewLeaseThread(blob, leaseId, _cancellationTokenSource.Token);
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

            private Thread StartNewAutoRenewLeaseThread(ICloudBlob blob, string leaseId, CancellationToken cancellationToken)
            {
                var autoRenewLeaseThread = new Thread(
                    async () =>
                    {
                        Trace.TraceInformation("Thread [{0}] started.", Thread.CurrentThread.ManagedThreadId);
                        var blobUriString = blob.Uri.ToString();
                        try
                        {
                            while (!cancellationToken.IsCancellationRequested &&
                                   await blob.ExistsAsync(cancellationToken))
                            {
                                // auto-renew lease when about to expire
                                Thread.Sleep(_leaseExpirationThreshold);

                                Trace.TraceInformation("Thread [{0}] working.", Thread.CurrentThread.ManagedThreadId);
                                Trace.TraceInformation("Beginning to renew lease for blob {0}.", blobUriString);

                                await blob.RenewLeaseAsync(AccessCondition.GenerateLeaseCondition(leaseId), cancellationToken);

                                Trace.TraceInformation("Finished to renew lease for blob {0}.", blobUriString);
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            Trace.TraceWarning("Thread [{0}] cancelled.", Thread.CurrentThread.ManagedThreadId);
                            // No need to track
                        }
                        catch
                        {
                            Trace.TraceError("Thread [{0}] error.", Thread.CurrentThread.ManagedThreadId);
                            // The blob could have been deleted in the meantime and this thread will be killed either way.
                            // No need to track in Application Insights.
                        }
                    });
                autoRenewLeaseThread.Start();
                return autoRenewLeaseThread;
            }

            public void Dispose()
            {
                if (_autoRenewLeaseThread != null)
                {
                    Trace.TraceInformation("Thread [{0}] disposing.", _autoRenewLeaseThread.ManagedThreadId);
                    _cancellationTokenSource.Cancel(false);
                    _autoRenewLeaseThread.Join();
                    Trace.TraceInformation("Thread [{0}] disposed.", _autoRenewLeaseThread.ManagedThreadId);
                }
            }
        }
    }
}
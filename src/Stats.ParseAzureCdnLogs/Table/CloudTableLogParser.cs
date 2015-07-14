// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Stats.AzureCdnLogs.Common;

namespace Stats.ParseAzureCdnLogs
{
    internal class CloudTableLogParser
    {
        private readonly TimeSpan _defaultLeaseTime = TimeSpan.FromSeconds(60);
        private readonly TimeSpan _leaseExpirationThreshold = TimeSpan.FromSeconds(40);
        private readonly CloudBlobContainer _targetContainer;
        private readonly CdnLogEntryTable _cdnLogEntryTable;
        private readonly PackageStatisticsTable _statisticsTable;
        private readonly CloudBlobContainer _deadLetterContainer;
        private readonly PackageStatisticsQueue _statisticsQueue;
        private readonly JobEventSource _jobEventSource;

        public CloudTableLogParser(JobEventSource jobEventSource, CloudBlobContainer targetContainer, CdnLogEntryTable cdnLogEntryTable,
            PackageStatisticsTable statisticsTable, CloudBlobContainer deadLetterContainer, PackageStatisticsQueue statisticsQueue)
        {
            _jobEventSource = jobEventSource;
            _targetContainer = targetContainer;
            _cdnLogEntryTable = cdnLogEntryTable;
            _statisticsTable = statisticsTable;
            _deadLetterContainer = deadLetterContainer;
            _statisticsQueue = statisticsQueue;
        }

        public async Task ParseLogFileAsync(CloudBlockBlob blob)
        {
            var sourceBlobExists = await blob.ExistsAsync();
            if (!sourceBlobExists)
            {
                return;
            }

            // try to acquire a lease on the blob
            string leaseId = await TryAcquireLeaseAsync(blob);
            if (string.IsNullOrEmpty(leaseId))
            {
                // the blob is already leased, ignore it and move on
                return;
            }

            // hold on to the lease for the duration of this method-action by auto-renewing in the background
            var autoRenewLeaseThread = StartNewAutoRenewLeaseThread(blob, leaseId);

            try
            {
                var blobUri = blob.Uri.ToString();
                var log = await DecompressBlobAsync(blob, blobUri, leaseId);

                await BatchInsertParsedLogEntriesAsync(blobUri, log);

                await ArchiveDecompressedBlobAsync(blob, blobUri, log);

                // delete the blob from the 'to-be-processed' container
                await DeleteSourceBlobAsync(blob, blobUri, leaseId);

                autoRenewLeaseThread.Abort();
            }
            catch (Exception e)
            {
                // avoid continuous rethrow and dead-letter the blob...
                autoRenewLeaseThread.Abort();
                // ... by taking an infinite lease (as long as the lease is there, no other job instance will be able to acquire a lease on it and attempt processing it)
                blob.AcquireLease(null, leaseId);

                // copy the blob to a dead-letter container
                var deadLetterBlob = _deadLetterContainer.GetBlockBlobReference(blob.Name);
                deadLetterBlob.StartCopyFromBlob(blob, AccessCondition.GenerateLeaseCondition(leaseId));

                // add the job error to the blob's metadata
                deadLetterBlob.FetchAttributes();
                deadLetterBlob.Metadata.Add("JobError", e.ToString());
                deadLetterBlob.SetMetadata();

                // delete the blob from the 'to-be-processed' container
                blob.DeleteIfExists(DeleteSnapshotsOption.IncludeSnapshots, AccessCondition.GenerateLeaseCondition(leaseId));
            }
        }

        private async Task ArchiveDecompressedBlobAsync(ICloudBlob blob, string blobUri, string log)
        {
            try
            {
                // stream the decompressed file to an archive container
                var decompressedBlobName = blob.Name.Replace(".gz", string.Empty);
                var targetBlob = _targetContainer.GetBlockBlobReference(decompressedBlobName);

                if (!await targetBlob.ExistsAsync())
                {
                    targetBlob.Properties.ContentType = "text/plain";
                    _jobEventSource.BeginningArchiveUpload(blobUri);
                    await targetBlob.UploadTextAsync(log);
                    _jobEventSource.FinishingArchiveUpload(blobUri);
                }
            }
            catch
            {
                _jobEventSource.FailedArchiveUpload(blobUri);
                throw;
            }
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
                        try
                        {
                            await blob.RenewLeaseAsync(AccessCondition.GenerateLeaseCondition(leaseId));
                        }
                        catch
                        {
                            // the blob could have been deleted in the meantime
                            // this thread will be killed either way
                        }
                    }
                });
            autoRenewLeaseThread.Start();
            return autoRenewLeaseThread;
        }

        private async Task BatchInsertParsedLogEntriesAsync(string blobUri, string log)
        {
            IReadOnlyCollection<CdnLogEntry> logEntries;
            IReadOnlyCollection<PackageStatistics> packageStatistics;
            try
            {
                // parse the text from memory into table entities
                _jobEventSource.BeginningParseLog(blobUri);
                logEntries = CdnLogEntryParser.ParseLogEntriesFromW3CLog(log);
                packageStatistics = PackageStatisticsParser.FromCdnLogEntries(logEntries);
                _jobEventSource.FinishingParseLog(blobUri);
            }
            catch
            {
                _jobEventSource.FailedParseLog(blobUri);
                throw;
            }

            try
            {
                // batch insert the parsed log entries into table storage
                _jobEventSource.BeginningBatchInsert(blobUri);
                await _cdnLogEntryTable.InsertBatchAsync(logEntries);
                await _statisticsTable.InsertOrReplaceBatchAsync(packageStatistics);

                foreach (var batch in SplitList(packageStatistics.ToDictionary(e => e.RowKey, e => e.PartitionKey)))
                {
                    var message = new PackageStatisticsQueueMessage();
                    message.PartitionAndRowKeys = batch.ToDictionary(e => e.Key, e => e.Value);
                    await _statisticsQueue.AddMessageAsync(message);
                }

                _jobEventSource.FinishingBatchInsert(blobUri);
            }
            catch
            {
                _jobEventSource.FailedBatchInsert(blobUri);
                throw;
            }
        }

        private async Task DeleteSourceBlobAsync(ICloudBlob blob, string blobUri, string leaseId)
        {
            if (await blob.ExistsAsync())
            {
                try
                {
                    _jobEventSource.BeginningDelete(blobUri);
                    var accessCondition = AccessCondition.GenerateLeaseCondition(leaseId);
                    await blob.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots, accessCondition, null, null);
                    _jobEventSource.FinishedDelete(blobUri);
                }
                catch
                {
                    _jobEventSource.FailedDelete(blobUri);
                    throw;
                }
            }
        }

        private async Task<string> DecompressBlobAsync(ICloudBlob blob, string blobUri, string leaseId)
        {
            string log;
            try
            {
                _jobEventSource.BeginningDecompressBlob(blobUri);
                using (var decompressedStream = new MemoryStream())
                {
                    // decompress into memory (these are rolling log files and relatively small)
                    using (var blobStream = await blob.OpenReadAsync(AccessCondition.GenerateLeaseCondition(leaseId), null, null))
                    using (var gzipStream = new GZipInputStream(blobStream))
                    {
                        await gzipStream.CopyToAsync(decompressedStream);
                    }

                    // reset the stream's position and read to end
                    decompressedStream.Position = 0;
                    using (var streamReader = new StreamReader(decompressedStream))
                    {
                        log = await streamReader.ReadToEndAsync();
                    }

                    _jobEventSource.FinishedDecompressBlob(blobUri);
                }
            }
            catch
            {
                _jobEventSource.FailedDecompressBlob(blobUri);
                throw;
            }

            return log;
        }

        private async Task<string> TryAcquireLeaseAsync(ICloudBlob blob)
        {
            string leaseId;
            var blobUriString = blob.Uri.ToString();
            try
            {
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

        public static List<IEnumerable<KeyValuePair<string, string>>> SplitList(IDictionary<string, string> source, int size = 250)
        {
            // pre-order by partition key and then by descending row key
            var orderedByPartitionKey = source.OrderBy(e => e.Value).ThenByDescending(e => e.Key).ToList();

            var splitted = new List<IEnumerable<KeyValuePair<string, string>>>();

            for (var i = 0; i < orderedByPartitionKey.Count; i += size)
            {
                splitted.Add(orderedByPartitionKey.Skip(i).Take(Math.Min(size, orderedByPartitionKey.Count - i)));
            }

            return splitted;
        }
    }
}
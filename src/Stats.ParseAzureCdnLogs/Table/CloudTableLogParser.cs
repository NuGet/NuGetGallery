// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly DateTime UnixTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private readonly TimeSpan _defaultLeaseTime = TimeSpan.FromSeconds(60);
        private readonly TimeSpan _leaseExpirationThreshold = TimeSpan.FromSeconds(40);
        private readonly CloudBlobContainer _targetContainer;
        private readonly CdnLogEntryTable _cdnLogEntryTable;
        private readonly PackageStatisticTable _statisticTable;
        private readonly CloudBlobContainer _deadLetterContainer;
        private readonly JobEventSource _jobEventSource;

        public CloudTableLogParser(JobEventSource jobEventSource, CloudBlobContainer targetContainer, CdnLogEntryTable cdnLogEntryTable,
            PackageStatisticTable statisticTable, CloudBlobContainer deadLetterContainer)
        {
            _jobEventSource = jobEventSource;
            _targetContainer = targetContainer;
            _cdnLogEntryTable = cdnLogEntryTable;
            _statisticTable = statisticTable;
            _deadLetterContainer = deadLetterContainer;
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
                        await blob.RenewLeaseAsync(AccessCondition.GenerateLeaseCondition(leaseId));
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
                logEntries = ParseLogEntriesFromW3CLog(log);
                packageStatistics = ParsePackageStatisticFromLogEntries(logEntries);
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
                await _statisticTable.InsertBatchAsync(packageStatistics);
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

        private static IReadOnlyCollection<CdnLogEntry> ParseLogEntriesFromW3CLog(string log)
        {
            var logEntries = new List<CdnLogEntry>();

            var logLines = log.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in logLines)
            {
                var logEntry = ParseLogEntryFromLine(line);
                if (logEntry != null)
                {
                    logEntries.Add(logEntry);
                }
            }

            return logEntries;
        }

        private static CdnLogEntry ParseLogEntryFromLine(string line)
        {
            // ignore comment rows (i.e., first row listing the column headers
            if (line.StartsWith("#"))
                return null;

            // columns are space-separated
            var columns = W3CParseUtils.GetLogLineRecords(line);

            var entry = new CdnLogEntry();

            // timestamp
            entry.EdgeServerTimeDelivered = FromUnixTimestamp(columns[0]);

            // time-taken
            TrySetIntProperty(value => entry.EdgeServerTimeTaken = value, columns[1]);

            // c-ip
            TrySetStringProperty(value => entry.ClientIpAddress = value, columns[2]);

            // filesize
            TrySetLongProperty(value => entry.FileSize = value, columns[3]);

            // s-ip
            TrySetStringProperty(value => entry.EdgeServerIpAddress = value, columns[4]);

            // s-port
            TrySetIntProperty(value => entry.EdgeServerPort = value, columns[5]);

            // sc-status
            TrySetStringProperty(value => entry.CacheStatusCode = value, columns[6]);

            // sc-bytes
            TrySetLongProperty(value => entry.EdgeServerBytesSent = value, columns[7]);

            // cs-method
            TrySetStringProperty(value => entry.HttpMethod = value, columns[8]);

            // cs-uri-stem
            TrySetStringProperty(value => entry.RequestUrl = value, columns[9]);

            // skip column 10, it just contains the '-' character

            // rs-duration
            TrySetIntProperty(value => entry.RemoteServerTimeTaken = value, columns[11]);

            // rs-bytes
            TrySetLongProperty(value => entry.RemoteServerBytesSent = value, columns[12]);

            // c-referrer
            TrySetStringProperty(value => entry.Referrer = value, columns[13]);

            // c-user-agent
            TrySetStringProperty(value => entry.UserAgent = value, columns[14]);

            // customer-id
            TrySetStringProperty(value => entry.CustomerId = value, columns[15]);

            // x-ec_custom-1
            TrySetStringProperty(value => entry.CustomField = value, columns[16]);

            return entry;
        }

        private static IReadOnlyCollection<PackageStatistics> ParsePackageStatisticFromLogEntries(IReadOnlyCollection<CdnLogEntry> logEntries)
        {
            var packageStatistics = new List<PackageStatistics>();

            foreach (var cdnLogEntry in logEntries)
            {
                var packageDefinition = PackageDefinition.FromRequestUrl(cdnLogEntry.RequestUrl);

                if (packageDefinition == null)
                {
                    continue;
                }

                var statistic = new PackageStatistics();

                // combination of partition- and row-key correlates each statistic to a cdn raw log entry
                statistic.PartitionKey = cdnLogEntry.PartitionKey;
                statistic.RowKey = cdnLogEntry.RowKey;
                statistic.EdgeServerTimeDelivered = cdnLogEntry.EdgeServerTimeDelivered;

                statistic.PackageId = packageDefinition.PackageId;
                statistic.PackageVersion = packageDefinition.PackageVersion;

                var customFieldDictionary = CdnLogCustomFieldParser.Parse(cdnLogEntry.CustomField);
                if (customFieldDictionary.ContainsKey(NuGetCustomHeaders.NuGetOperation))
                {
                    var operation = customFieldDictionary[NuGetCustomHeaders.NuGetOperation];
                    if (!string.Equals("-", operation))
                    {
                        statistic.Operation = operation;
                    }
                }
                if (customFieldDictionary.ContainsKey(NuGetCustomHeaders.NuGetDependentPackage))
                {
                    var dependentPackage = customFieldDictionary[NuGetCustomHeaders.NuGetDependentPackage];
                    if (!string.Equals("-", dependentPackage))
                    {
                        statistic.DependentPackage = dependentPackage;
                    }
                }
                if (customFieldDictionary.ContainsKey(NuGetCustomHeaders.NuGetProjectGuids))
                {
                    var dependentPackage = customFieldDictionary[NuGetCustomHeaders.NuGetProjectGuids];
                    if (!string.Equals("-", dependentPackage))
                    {
                        statistic.ProjectGuids = dependentPackage;
                    }
                }

                if (cdnLogEntry.UserAgent.StartsWith("\"") && cdnLogEntry.UserAgent.EndsWith("\""))
                {
                    statistic.UserAgent = cdnLogEntry.UserAgent.Substring(1, cdnLogEntry.UserAgent.Length - 2);
                }
                else
                {
                    statistic.UserAgent = cdnLogEntry.UserAgent;
                }

                packageStatistics.Add(statistic);
            }

            return packageStatistics;
        }

        private static void TrySetLongProperty(Action<long?> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(long.Parse(record));
            }
        }

        private static void TrySetIntProperty(Action<int?> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(int.Parse(record));
            }
        }

        private static void TrySetStringProperty(Action<string> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(record);
            }
        }

        private static DateTime FromUnixTimestamp(string unixTimestamp)
        {
            // Unix timestamp is seconds past epoch
            var secondsPastEpoch = double.Parse(unixTimestamp);
            return UnixTimestamp + TimeSpan.FromSeconds(secondsPastEpoch);
        }
    }
}
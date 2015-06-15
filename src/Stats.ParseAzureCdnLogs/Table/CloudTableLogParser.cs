// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.ParseAzureCdnLogs
{
    internal class CloudTableLogParser
    {
        private static readonly DateTime UnixTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private readonly TimeSpan _defaultLeaseTime = TimeSpan.FromSeconds(60);
        private readonly TimeSpan _leaseExpirationThreshold = TimeSpan.FromSeconds(40);
        private readonly CloudBlobContainer _targetContainer;
        private readonly CdnLogEntryTable _targetTable;
        private readonly JobEventSource _jobEventSource;

        public CloudTableLogParser(JobEventSource jobEventSource, CloudBlobContainer targetContainer, CdnLogEntryTable targetTable)
        {
            _jobEventSource = jobEventSource;
            _targetContainer = targetContainer;
            _targetTable = targetTable;
        }

        public async Task ParseLogFileAsync(CloudBlockBlob blob)
        {
            // acquire and hold on to the lease for the duration of this method-action
            var leaseId = await blob.AcquireLeaseAsync(_defaultLeaseTime, null);
            Thread autoRenewLeaseThread = null;
            if (!string.IsNullOrEmpty(leaseId))
            {
                autoRenewLeaseThread = new Thread(
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
            }

            try
            {
                string log;
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
                }

                // parse the text from memory into table storage
                IEnumerable<CdnLogEntry> logEntries = ParseLogEntriesFromW3CLog(log);
                await _targetTable.InsertBatchAsync(logEntries);

                // stream the decompressed file to an archive container
                var targetBlob = _targetContainer.GetBlockBlobReference(blob.Name.Replace(".gz", string.Empty));
                targetBlob.Properties.ContentType = "text/plain";
                await targetBlob.UploadTextAsync(log);

                // delete the gzipped file from the 'to-be-processed' container
                await blob.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots, AccessCondition.GenerateLeaseCondition(leaseId), null, null);
            }
            finally
            {
                if (autoRenewLeaseThread != null)
                {
                    autoRenewLeaseThread.Abort();
                }
            }
        }

        private static IEnumerable<CdnLogEntry> ParseLogEntriesFromW3CLog(string log)
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
            var columns = line.Split(new[] { " " }, StringSplitOptions.None);

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

        private static void TrySetLongProperty(Action<long?> propertySetter, string record)
        {
            if (W3CLogRecordContainsData(record))
            {
                propertySetter(long.Parse(record));
            }
        }

        private static void TrySetIntProperty(Action<int?> propertySetter, string record)
        {
            if (W3CLogRecordContainsData(record))
            {
                propertySetter(int.Parse(record));
            }
        }

        private static void TrySetStringProperty(Action<string> propertySetter, string record)
        {
            if (W3CLogRecordContainsData(record))
            {
                propertySetter(record);
            }
        }

        private static bool W3CLogRecordContainsData(string record)
        {
            return !string.IsNullOrWhiteSpace(record) && record != "-" && record != "\"-\"";
        }

        private static DateTime FromUnixTimestamp(string unixTimestamp)
        {
            // Unix timestamp is seconds past epoch
            var secondsPastEpoch = double.Parse(unixTimestamp);
            return UnixTimestamp + TimeSpan.FromSeconds(secondsPastEpoch);
        }
    }
}
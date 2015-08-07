// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    internal class LogFileProcessor
    {
        private readonly CloudBlobContainer _targetContainer;
        private readonly CloudBlobContainer _deadLetterContainer;
        private readonly SqlConnectionStringBuilder _targetDatabase;
        private readonly JobEventSource _jobEventSource = JobEventSource.Log;

        public LogFileProcessor(CloudBlobContainer targetContainer, CloudBlobContainer deadLetterContainer, SqlConnectionStringBuilder targetDatabase)
        {
            _targetContainer = targetContainer;
            _deadLetterContainer = deadLetterContainer;
            _targetDatabase = targetDatabase;
        }

        public async Task ProcessLogFileAsync(ILeasedLogFile logFile)
        {
            if (logFile == null)
                return;

            try
            {
                var log = await DecompressBlobAsync(logFile);
                var packageStatistics = ParseLogEntries(logFile.Uri, log, logFile.Blob.Name);

                if (packageStatistics.Any())
                {
                    // replicate data to the statistics database
                    var warehouse = new Warehouse(_jobEventSource, _targetDatabase);
                    await warehouse.InsertDownloadFactsAsync(packageStatistics, logFile.Blob.Name);
                }

                await ArchiveBlobAsync(logFile);

                // delete the blob from the 'to-be-processed' container
                await DeleteSourceBlobAsync(logFile);
            }
            catch (Exception e)
            {
                await _deadLetterContainer.CreateIfNotExistsAsync();

                // copy the blob to a dead-letter container
                await EnsureCopiedToContainerAsync(logFile, _deadLetterContainer, e);

                // delete the blob from the 'to-be-processed' container
                await DeleteSourceBlobAsync(logFile);
            }
        }

        private static async Task EnsureCopiedToContainerAsync(ILeasedLogFile logFile, CloudBlobContainer targetContainer, Exception e = null)
        {
            var archivedBlob = targetContainer.GetBlockBlobReference(logFile.Blob.Name);
            if (!await archivedBlob.ExistsAsync())
            {
                await archivedBlob.StartCopyFromBlobAsync(logFile.Blob);

                archivedBlob = (CloudBlockBlob)await targetContainer.GetBlobReferenceFromServerAsync(logFile.Blob.Name);

                while (archivedBlob.CopyState.Status == CopyStatus.Pending)
                {
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                    archivedBlob = (CloudBlockBlob)await targetContainer.GetBlobReferenceFromServerAsync(logFile.Blob.Name);
                }

                await archivedBlob.FetchAttributesAsync();

                if (e != null)
                {
                    // add the job error to the blob's metadata
                    if (archivedBlob.Metadata.ContainsKey("JobError"))
                    {
                        archivedBlob.Metadata["JobError"] = e.ToString().Replace("\r\n", string.Empty);
                    }
                    else
                    {
                        archivedBlob.Metadata.Add("JobError", e.ToString().Replace("\r\n", string.Empty));
                    }
                    await archivedBlob.SetMetadataAsync();
                }
                else if (archivedBlob.Metadata.ContainsKey("JobError"))
                {
                    archivedBlob.Metadata.Remove("JobError");
                    await archivedBlob.SetMetadataAsync();
                }
            }
        }

        private IReadOnlyCollection<PackageStatistics> ParseLogEntries(string blobUri, string log, string blobName)
        {
            IReadOnlyCollection<PackageStatistics> packageStatistics;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // parse the text from memory into table entities
                _jobEventSource.BeginningParseLog(blobUri);
                var logEntries = CdnLogEntryParser.ParseLogEntriesFromW3CLog(log);
                packageStatistics = PackageStatisticsParser.FromCdnLogEntries(logEntries);
                _jobEventSource.FinishingParseLog(blobUri, packageStatistics.Count);

                stopwatch.Stop();
                ApplicationInsights.TrackMetric("Blob parsing duration (ms)", stopwatch.ElapsedMilliseconds, blobName);
            }
            catch (Exception exception)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                _jobEventSource.FailedParseLog(blobUri);
                ApplicationInsights.TrackException(exception, blobName);
                throw;
            }

            return packageStatistics;
        }

        private async Task<string> DecompressBlobAsync(ILeasedLogFile logFile)
        {
            string log;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _jobEventSource.BeginningDecompressBlob(logFile.Uri);

                using (var decompressedStream = new MemoryStream())
                {
                    // decompress into memory (these are rolling log files and relatively small)
                    using (var blobStream = await logFile.Blob.OpenReadAsync(AccessCondition.GenerateLeaseCondition(logFile.LeaseId), null, null))
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

                    stopwatch.Stop();

                    _jobEventSource.FinishedDecompressBlob(logFile.Uri);

                    ApplicationInsights.TrackMetric("Blob decompression duration (ms)", stopwatch.ElapsedMilliseconds, logFile.Blob.Name);
                }
            }
            catch (Exception exception)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                _jobEventSource.FailedDecompressBlob(logFile.Uri);
                ApplicationInsights.TrackException(exception, logFile.Blob.Name);
                throw;
            }

            return log;
        }

        private async Task ArchiveBlobAsync(ILeasedLogFile logFile)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await EnsureCopiedToContainerAsync(logFile, _targetContainer);

                _jobEventSource.FinishingArchiveUpload(logFile.Uri);

                stopwatch.Stop();
                ApplicationInsights.TrackMetric("Blob archiving duration (ms)", stopwatch.ElapsedMilliseconds, logFile.Blob.Name);
            }
            catch (Exception exception)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                _jobEventSource.FailedArchiveUpload(logFile.Uri);
                ApplicationInsights.TrackException(exception, logFile.Blob.Name);
                throw;
            }
        }

        private async Task DeleteSourceBlobAsync(ILeasedLogFile logFile)
        {
            if (await logFile.Blob.ExistsAsync())
            {
                try
                {
                    _jobEventSource.BeginningDelete(logFile.Uri);
                    var accessCondition = AccessCondition.GenerateLeaseCondition(logFile.LeaseId);
                    await logFile.Blob.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots, accessCondition, null, null);
                    _jobEventSource.FinishedDelete(logFile.Uri);
                }
                catch (Exception exception)
                {
                    _jobEventSource.FailedDelete(logFile.Uri);
                    ApplicationInsights.TrackException(exception, logFile.Blob.Name);
                    throw;
                }
            }
        }

    }
}
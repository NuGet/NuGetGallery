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
        private const ushort GzipLeadBytes = 0x8b1f;

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
                var cdnStatistics = await ParseLogEntries(logFile);
                var hasPackageStatistics = cdnStatistics.PackageStatistics.Any();
                var hasToolStatistics = cdnStatistics.ToolStatistics.Any();

                if (hasPackageStatistics || hasToolStatistics)
                {
                    // replicate data to the statistics database
                    var warehouse = new Warehouse(_jobEventSource, _targetDatabase);

                    if (hasPackageStatistics)
                    {
                        var downloadFacts = await warehouse.CreateAsync(cdnStatistics.PackageStatistics, logFile.Blob.Name);
                        await warehouse.InsertDownloadFactsAsync(downloadFacts, logFile.Blob.Name);
                    }
                    if (hasToolStatistics)
                    {
                        var downloadFacts = await warehouse.CreateAsync(cdnStatistics.ToolStatistics, logFile.Blob.Name);
                        await warehouse.InsertDownloadFactsAsync(downloadFacts, logFile.Blob.Name);
                    }
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

        private async Task<CdnStatistics> ParseLogEntries(ILeasedLogFile logFile)
        {
            var logStream = await OpenCompressedBlobAsync(logFile);
            var blobUri = logFile.Uri;
            var blobName = logFile.Blob.Name;

            var packageStatistics = new List<PackageStatistics>();
            var toolStatistics = new List<ToolStatistics>();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // parse the log into table entities
                _jobEventSource.BeginningParseLog(blobUri);

                using (var logStreamReader = new StreamReader(logStream))
                {
                    do
                    {
                        var rawLogLine = logStreamReader.ReadLine();
                        if (rawLogLine != null)
                        {
                            var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(rawLogLine);
                            if (logEntry != null)
                            {
                                var statistic = PackageStatisticsParser.FromCdnLogEntry(logEntry);
                                if (statistic != null)
                                {
                                    packageStatistics.Add(statistic);
                                }
                                else
                                {
                                    // check if this is a dist.nuget.org download
                                    if (logEntry.RequestUrl.Contains("dist.nuget.org/"))
                                    {
                                        var toolInfo = ToolStatisticsParser.FromCdnLogEntry(logEntry);
                                        toolStatistics.Add(toolInfo);
                                    }
                                }
                            }
                        }
                    } while (!logStreamReader.EndOfStream);
                }

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
            finally
            {
                logStream.Dispose();
            }


            var cdnStatistics = new CdnStatistics(packageStatistics, toolStatistics);
            return cdnStatistics;
        }

        private static async Task<bool> IsGzipCompressed(Stream stream)
        {
            stream.Position = 0;

            try
            {
                var bytes = new byte[4];
                await stream.ReadAsync(bytes, 0, 4);

                return (BitConverter.ToUInt16(bytes, 0) == GzipLeadBytes);
            }
            finally
            {
                stream.Position = 0;
            }
        }

        private async Task<Stream> OpenCompressedBlobAsync(ILeasedLogFile logFile)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _jobEventSource.BeginningOpenCompressedBlob(logFile.Uri);

                var memoryStream = new MemoryStream();

                // decompress into memory (these are rolling log files and relatively small)
                using (var blobStream = await logFile.Blob.OpenReadAsync(AccessCondition.GenerateLeaseCondition(logFile.LeaseId), null, null))
                {
                    await blobStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                }

                stopwatch.Stop();

                _jobEventSource.FinishedOpenCompressedBlob(logFile.Uri);

                ApplicationInsights.TrackMetric("Open compressed blob duration (ms)", stopwatch.ElapsedMilliseconds, logFile.Blob.Name);

                // verify if the stream is gzipped or not
                if (await IsGzipCompressed(memoryStream))
                {
                    return new GZipInputStream(memoryStream);
                }
                else
                {
                    return memoryStream;
                }
            }
            catch (Exception exception)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                _jobEventSource.FailedOpenCompressedBlob(logFile.Uri);
                ApplicationInsights.TrackException(exception, logFile.Blob.Name);
                throw;
            }
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
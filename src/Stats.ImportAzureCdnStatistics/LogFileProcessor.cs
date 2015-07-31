// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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
            try
            {
                var log = await DecompressBlobAsync(logFile);
                var packageStatistics = ParseLogEntries(logFile.Uri, log);

                if (packageStatistics.Any())
                {
                    // replicate data to the statistics database
                    var warehouse = new Warehouse(_jobEventSource, _targetDatabase);
                    await warehouse.InsertDownloadFactsAsync(packageStatistics, logFile.Blob.Name);
                }

                await ArchiveDecompressedBlobAsync(logFile, log);

                // delete the blob from the 'to-be-processed' container
                await DeleteSourceBlobAsync(logFile);
            }
            catch (Exception e)
            {
                // avoid continuous rethrow and dead-letter the blob...
                await logFile.AcquireInfiniteLeaseAsync();

                // copy the blob to a dead-letter container
                var deadLetterBlob = _deadLetterContainer.GetBlockBlobReference(logFile.Blob.Name);
                await deadLetterBlob.StartCopyFromBlobAsync(logFile.Blob);

                // add the job error to the blob's metadata
                await deadLetterBlob.FetchAttributesAsync();
                deadLetterBlob.Metadata.Add("JobError", e.ToString().Replace("\r\n", string.Empty));
                await deadLetterBlob.SetMetadataAsync();

                // delete the blob from the 'to-be-processed' container
                await logFile.Blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, AccessCondition.GenerateLeaseCondition(logFile.LeaseId), null, null);
            }
        }

        private IReadOnlyCollection<PackageStatistics> ParseLogEntries(string blobUri, string log)
        {
            IReadOnlyCollection<PackageStatistics> packageStatistics;

            try
            {
                // parse the text from memory into table entities
                _jobEventSource.BeginningParseLog(blobUri);
                var logEntries = CdnLogEntryParser.ParseLogEntriesFromW3CLog(log);
                packageStatistics = PackageStatisticsParser.FromCdnLogEntries(logEntries);
                _jobEventSource.FinishingParseLog(blobUri, packageStatistics.Count);
            }
            catch
            {
                _jobEventSource.FailedParseLog(blobUri);
                throw;
            }

            return packageStatistics;
        }

        private async Task<string> DecompressBlobAsync(ILeasedLogFile logFile)
        {
            string log;
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

                    _jobEventSource.FinishedDecompressBlob(logFile.Uri);
                }
            }
            catch
            {
                _jobEventSource.FailedDecompressBlob(logFile.Uri);
                throw;
            }

            return log;
        }

        private async Task ArchiveDecompressedBlobAsync(ILeasedLogFile logFile, string log)
        {
            try
            {
                // stream the decompressed file to an archive container
                var decompressedBlobName = logFile.Blob.Name.Replace(".gz", string.Empty);
                var targetBlob = _targetContainer.GetBlockBlobReference(decompressedBlobName);

                if (!await targetBlob.ExistsAsync())
                {
                    targetBlob.Properties.ContentType = "text/plain";
                    _jobEventSource.BeginningArchiveUpload(logFile.Uri);
                    await targetBlob.UploadTextAsync(log);
                    _jobEventSource.FinishingArchiveUpload(logFile.Uri);
                }
            }
            catch
            {
                _jobEventSource.FailedArchiveUpload(logFile.Uri);
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
                catch
                {
                    _jobEventSource.FailedDelete(logFile.Uri);
                    throw;
                }
            }
        }

    }
}
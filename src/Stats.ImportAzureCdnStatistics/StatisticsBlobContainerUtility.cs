// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    internal class StatisticsBlobContainerUtility
        : IStatisticsBlobContainerUtility
    {
        private readonly CloudBlobContainer _targetContainer;
        private readonly CloudBlobContainer _deadLetterContainer;
        private readonly ApplicationInsightsHelper _applicationInsightsHelper;
        private readonly ILogger _logger;
        private const ushort _gzipLeadBytes = 0x8b1f;
        private const string _jobErrorMetadataKey = "JobError";

        internal StatisticsBlobContainerUtility(
            CloudBlobContainer targetContainer,
            CloudBlobContainer deadLetterContainer,
            ILoggerFactory loggerFactory,
            ApplicationInsightsHelper applicationInsightsHelper)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _targetContainer = targetContainer ?? throw new ArgumentNullException(nameof(targetContainer));
            _deadLetterContainer = deadLetterContainer ?? throw new ArgumentNullException(nameof(deadLetterContainer));
            _applicationInsightsHelper = applicationInsightsHelper ?? throw new ArgumentNullException(nameof(applicationInsightsHelper));
            _logger = loggerFactory.CreateLogger<StatisticsBlobContainerUtility>();
        }

        public async Task CopyToDeadLetterContainerAsync(ILeasedLogFile logFile, Exception e)
        {
            await CopyToContainerAsync(logFile, _deadLetterContainer, e);
        }

        public async Task DeleteSourceBlobAsync(ILeasedLogFile logFile)
        {
            if (await logFile.Blob.ExistsAsync())
            {
                try
                {
                    _logger.LogInformation("Beginning to delete blob {FtpBlobUri}.", logFile.Uri);

                    var accessCondition = AccessCondition.GenerateLeaseCondition(logFile.LeaseId);
                    await logFile.Blob.DeleteAsync(
                        DeleteSnapshotsOption.IncludeSnapshots,
                        accessCondition,
                        options: null,
                        operationContext: null);

                    _logger.LogInformation("Finished to delete blob {FtpBlobUri}.", logFile.Uri);
                }
                catch (Exception exception)
                {
                    _logger.LogError(LogEvents.FailedBlobDelete, exception, "Failed to delete blob {FtpBlobUri}", logFile.Uri);
                    _applicationInsightsHelper.TrackException(exception, logFile.Blob.Name);
                    throw;
                }
            }
        }

        public async Task ArchiveBlobAsync(ILeasedLogFile logFile)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                await CopyToTargetContainerAsync(logFile);

                _logger.LogInformation("Finished archive upload for blob {FtpBlobUri}.", logFile.Uri);

                stopwatch.Stop();
                _applicationInsightsHelper.TrackMetric("Blob archiving duration (ms)", stopwatch.ElapsedMilliseconds, logFile.Blob.Name);
            }
            catch (Exception exception)
            {
                _logger.LogError(LogEvents.FailedBlobUpload, exception, "Failed archive upload for blob {FtpBlobUri}", logFile.Uri);
                _applicationInsightsHelper.TrackException(exception, logFile.Blob.Name);
                throw;
            }
        }

        public async Task<Stream> OpenCompressedBlobAsync(ILeasedLogFile logFile)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                _logger.LogInformation("Beginning opening of compressed blob {FtpBlobUri}.", logFile.Uri);

                var memoryStream = new MemoryStream();

                // decompress into memory (these are rolling log files and relatively small)
                using (var blobStream = await logFile.Blob.OpenReadAsync(AccessCondition.GenerateLeaseCondition(logFile.LeaseId), null, null))
                {
                    await blobStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                }

                stopwatch.Stop();

                _logger.LogInformation("Finished opening of compressed blob {FtpBlobUri}.", logFile.Uri);

                _applicationInsightsHelper.TrackMetric("Open compressed blob duration (ms)", stopwatch.ElapsedMilliseconds, logFile.Blob.Name);

                // verify if the stream is gzipped or not
                if (await IsGzipCompressedAsync(memoryStream))
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
                _logger.LogError(LogEvents.FailedToDecompressBlob, exception, "Failed to open compressed blob {FtpBlobUri}", logFile.Uri);
                _applicationInsightsHelper.TrackException(exception, logFile.Blob.Name);

                throw;
            }
        }

        private static async Task<bool> IsGzipCompressedAsync(Stream stream)
        {
            stream.Position = 0;

            try
            {
                var bytes = new byte[4];
                await stream.ReadAsync(bytes, 0, 4);

                return BitConverter.ToUInt16(bytes, 0) == _gzipLeadBytes;
            }
            finally
            {
                stream.Position = 0;
            }
        }

        private async Task CopyToTargetContainerAsync(ILeasedLogFile logFile)
        {
            await CopyToContainerAsync(logFile, _targetContainer);
        }

        private static async Task CopyToContainerAsync(
            ILeasedLogFile logFile,
            CloudBlobContainer container,
            Exception e = null)
        {
            var archivedBlob = container.GetBlockBlobReference(logFile.Blob.Name);
            if (!await archivedBlob.ExistsAsync())
            {
                await archivedBlob.StartCopyAsync(logFile.Blob);

                archivedBlob = (CloudBlockBlob)await container.GetBlobReferenceFromServerAsync(logFile.Blob.Name);

                while (archivedBlob.CopyState.Status == CopyStatus.Pending)
                {
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                    archivedBlob = (CloudBlockBlob)await container.GetBlobReferenceFromServerAsync(logFile.Blob.Name);
                }

                await archivedBlob.FetchAttributesAsync();

                if (e != null)
                {
                    // add the job error to the blob's metadata
                    archivedBlob.Metadata[_jobErrorMetadataKey] = e.ToString().Replace("\r\n", string.Empty);

                    await archivedBlob.SetMetadataAsync();
                }
                else if (archivedBlob.Metadata.ContainsKey(_jobErrorMetadataKey))
                {
                    archivedBlob.Metadata.Remove(_jobErrorMetadataKey);
                    await archivedBlob.SetMetadataAsync();
                }
            }
        }
    }
}
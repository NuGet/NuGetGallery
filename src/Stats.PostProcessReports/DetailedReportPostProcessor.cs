// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Services.Storage;

namespace Stats.PostProcessReports
{
    public class DetailedReportPostProcessor : IDetailedReportPostProcessor
    {
        private const string JsonContentType = "application/json";
        private const string TextContentType = "text/plain";
        private const string SuccessFilename = "_SUCCESS";
        private const string CopySucceededFilename = "_WorkCopySucceeded";
        private const string JobSucceededFilename = "_JobSucceeded";
        private const string JsonExtension = ".json";
        private const string TotalLinesMetadataItem = "TotalLines";
        private const string LinesFailedMetadataItem = "LinesFailed";
        private const string FilesCreatedMetadataItem = "FilesCreated";
        private readonly IStorage _sourceStorage;
        private readonly IStorage _workStorage;
        private readonly IStorage _destinationStorage;
        private readonly PostProcessReportsConfiguration _configuration;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<DetailedReportPostProcessor> _logger;

        public DetailedReportPostProcessor(
            IStorage sourceStorage,
            IStorage workStorage,
            IStorage destinationStorage,
            IOptionsSnapshot<PostProcessReportsConfiguration> configurationAccessor,
            ITelemetryService telemetryService,
            ILogger<DetailedReportPostProcessor> logger)
        {
            _sourceStorage = sourceStorage ?? throw new ArgumentNullException(nameof(sourceStorage));
            _workStorage = workStorage ?? throw new ArgumentNullException(nameof(workStorage));
            _destinationStorage = destinationStorage ?? throw new ArgumentNullException(nameof(destinationStorage));
            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }
            _configuration = configurationAccessor.Value ?? throw new ArgumentException($"{nameof(configurationAccessor.Value)} property must not be null", nameof(configurationAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger.LogInformation("Connection limit: {ConnectionLimit}", ServicePointManager.DefaultConnectionLimit);

            // there are HTTP requests sent outside of worker threads, so we'll buffer the target connection
            // limit a bit to accommodate for anything going slightly unexpected.
            ServicePointManager.DefaultConnectionLimit = _configuration.ReportWriteDegreeOfParallelism + 10;
        }

        public async Task CopyReportsAsync()
        {
            var cancellationToken = CancellationToken.None;
            var sourceBlobs = await EnumerateSourceBlobsAsync();

            var sourceSuccessFile = sourceBlobs.Where(b => GetBlobName(b) == SuccessFilename).FirstOrDefault();

            if (sourceSuccessFile == null)
            {
                _logger.LogInformation("No " + SuccessFilename + " file present in source location, terminating until file is available.");
                return;
            }

            if (sourceSuccessFile.LastModifiedUtc.HasValue)
            {
                var ageHours = (DateTime.UtcNow - sourceSuccessFile.LastModifiedUtc.Value).TotalHours;
                _telemetryService.ReportSourceAge(ageHours);
            }

            var copySucceeded = await _workStorage.ExistsAsync(CopySucceededFilename, cancellationToken);

            var sourceJsonBlobs = sourceBlobs.Where(b => b.Uri.AbsolutePath.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase)).ToList();
            var copyNeeded = true;
            var sourceBlobsExist = false;
            if (copySucceeded)
            {
                sourceBlobsExist = await AnySourceBlobExistsInWorkLocationAsync(sourceJsonBlobs, cancellationToken);
                copyNeeded = !sourceBlobsExist;
            }

            if (copyNeeded)
            {
                await CopySourceBlobsAsync(sourceJsonBlobs, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Previous copy succeeded, will not copy.");
            }

            var jobSucceeded = await _workStorage.ExistsAsync(JobSucceededFilename, cancellationToken);
            if (copySucceeded && jobSucceeded && sourceBlobsExist)
            {
                _logger.LogInformation("No new data, terminating until updated.");
                return;
            }

            var workBlobs = await EnumerateWorkStorageBlobsAsync();
            var workJsonBlobs = workBlobs.Where(b => b.Uri.AbsolutePath.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase)).ToList();
            await ProcessBlobs(workJsonBlobs, cancellationToken);

            _logger.LogInformation("Done processing");
        }

        private async Task ProcessBlobs(List<StorageListItem> jsonBlobs, CancellationToken cancellationToken)
        {
            var totals = new TotalStats();
            foreach (var sourceBlob in jsonBlobs)
            {
                var blobName = GetBlobName(sourceBlob);
                var workBlobUri = _workStorage.ResolveUri(blobName);
                var sourceBlobStats = new BlobStatistics();
                var individualReports = await ProcessSourceBlobAsync(sourceBlob, sourceBlobStats, totals);
                using (_logger.BeginScope("Processing {BlobName}", blobName))
                {
                    if (individualReports != null && individualReports.Any())
                    {
                        var consumerTasks = Enumerable
                            .Range(1, _configuration.ReportWriteDegreeOfParallelism)
                            .Select(instanceId => WriteReports(instanceId, individualReports, sourceBlobStats, blobName, cancellationToken))
                            .ToList();

                        await Task.WhenAll(consumerTasks);
                        ++totals.SourceFilesProcessed;
                        totals.TotalLinesProcessed += sourceBlobStats.TotalLineCount;
                        totals.TotalLinesFailed += sourceBlobStats.LinesFailed;
                        totals.TotalFilesCreated += sourceBlobStats.FilesCreated;
                        var metadata = new Dictionary<string, string>
                        {
                            { TotalLinesMetadataItem, sourceBlobStats.TotalLineCount.ToString() },
                            { LinesFailedMetadataItem, sourceBlobStats.LinesFailed.ToString() },
                            { FilesCreatedMetadataItem, sourceBlobStats.FilesCreated.ToString() },
                        };
                        await _workStorage.SetMetadataAsync(workBlobUri, metadata);
                        _logger.LogInformation(
                            "Finished processing {BlobName}: total lines: {TotalLines}, created {FilesCreated} files, failed to parse {FailedLines} lines",
                            blobName,
                            sourceBlobStats.TotalLineCount,
                            sourceBlobStats.FilesCreated,
                            sourceBlobStats.LinesFailed);
                        _telemetryService.ReportFileProcessed(sourceBlobStats.TotalLineCount, sourceBlobStats.FilesCreated, sourceBlobStats.LinesFailed);
                    }
                }
            }
            var jobSucceededUrl = _workStorage.ResolveUri(JobSucceededFilename);
            var jobSucceededContent = new StringStorageContent("", TextContentType);
            await _workStorage.Save(jobSucceededUrl, jobSucceededContent, overwrite: true, cancellationToken: cancellationToken);
            _telemetryService.ReportTotals(totals.SourceFilesProcessed, totals.TotalLinesProcessed, totals.TotalFilesCreated, totals.TotalLinesFailed);
        }

        private async Task<bool> AnySourceBlobExistsInWorkLocationAsync(List<StorageListItem> jsonBlobs, CancellationToken cancellationToken)
        {
            var jsonBlob = jsonBlobs.Select(b => GetBlobName(b)).FirstOrDefault();
            if (jsonBlob != null)
            {
                return await _workStorage.ExistsAsync(jsonBlob, cancellationToken);
            }
            return false;
        }

        /// <summary>
        /// Sorting helper to make sure when we are cleaning up work location, flag
        /// files are deleted first, so we don't have a chance to end up in an inconsistent
        /// state if deletion fails at some point.
        /// </summary>
        private static int FlagFilesFirst(StorageListItem listItem)
        {
            var blobName = GetBlobName(listItem);
            if (blobName == CopySucceededFilename)
            {
                return 0;
            }
            if (blobName == JobSucceededFilename)
            {
                return 1;
            }

            return 2;
        }

        private async Task CopySourceBlobsAsync(List<StorageListItem> jsonBlobs, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cleaning up work storage");
            var workFilesToDelete = (await EnumerateWorkStorageBlobsAsync()).OrderBy(FlagFilesFirst).ToList();
            foreach (var existingWorkBlob in workFilesToDelete)
            {
                await _workStorage.Delete(existingWorkBlob.Uri, cancellationToken);
            }
            _logger.LogInformation("Deleted {NumDeletedFiles} files from work storage", workFilesToDelete.Count);
            foreach (var sourceBlob in jsonBlobs)
            {
                var blobName = GetBlobName(sourceBlob);
                var targetUrl = _workStorage.ResolveUri(blobName);
                _logger.LogInformation("{SourceBlobUri} ({BlobName})", sourceBlob.Uri.AbsoluteUri, blobName);
                _logger.LogInformation("{WorkBlobUrl}", targetUrl);
                await _sourceStorage.CopyAsync(sourceBlob.Uri, _workStorage, targetUrl, destinationProperties: null, cancellationToken);
            }
            var copySucceededContent = new StringStorageContent("", TextContentType);
            var copySucceededUrl = _workStorage.ResolveUri(CopySucceededFilename);
            await _workStorage.Save(copySucceededUrl, copySucceededContent, overwrite: true, cancellationToken: cancellationToken);
        }

        private async Task<List<StorageListItem>> EnumerateSourceBlobsAsync()
        {
            var blobs = await _sourceStorage.List(getMetadata: true, cancellationToken: CancellationToken.None);

            return blobs.ToList();
        }

        private async Task<List<StorageListItem>> EnumerateWorkStorageBlobsAsync()
        {
            var blobs = await _workStorage.List(getMetadata: true, cancellationToken: CancellationToken.None);

            return blobs.ToList();
        }

        private class PackageIdContainer
        {
            public string PackageId { get; set; }
        };

        private async Task<ConcurrentBag<LineProcessingContext>> ProcessSourceBlobAsync(
            StorageListItem sourceBlob,
            BlobStatistics blobStats,
            TotalStats totalStats)
        {
            _logger.LogInformation("Processing {BlobUrl}", sourceBlob.Uri.AbsoluteUri);
            if (BlobMetadataExists(sourceBlob, totalStats))
            {
                _logger.LogInformation("Blob metadata indicates blob has been processed already, skipping.");
                return null;
            }
            var sw = Stopwatch.StartNew();
            var numLines = 0;
            var individualReports = new ConcurrentBag<LineProcessingContext>();
            var workStorageUrl = _workStorage.ResolveUri(GetBlobName(sourceBlob));
            var storageContent = await _workStorage.Load(workStorageUrl, CancellationToken.None);
            using (var sourceStream = storageContent.GetContentStream())
            using (var streamReader = new StreamReader(sourceStream))
            {
                string line;
                while ((line = await streamReader.ReadLineAsync()) != null)
                {
                    ++numLines;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    individualReports.Add(new LineProcessingContext
                    {
                        LineNumber = numLines,
                        Data = line,
                    });
                }
            }
            sw.Stop();
            blobStats.TotalLineCount = numLines;
            _logger.LogInformation("Read {NumLines} lines from {BlobUrl} in {Elapsed}",
                numLines,
                sourceBlob.Uri.AbsoluteUri,
                sw.Elapsed);
            return individualReports;
        }

        private static bool BlobMetadataExists(StorageListItem sourceBlob, TotalStats totalStats)
        {
            if (sourceBlob.Metadata == null)
            {
                return false;
            }

            var allMetadataExists = sourceBlob.Metadata.TryGetValue(TotalLinesMetadataItem, out var totalLinesStr);
            allMetadataExists = sourceBlob.Metadata.TryGetValue(LinesFailedMetadataItem, out var linesFailedStr) && allMetadataExists;
            allMetadataExists = sourceBlob.Metadata.TryGetValue(FilesCreatedMetadataItem, out var filesCreatedStr) && allMetadataExists;

            if (!allMetadataExists)
            {
                // If not all metadata is present, will not update totals and will reprocess the file.
                return false;
            }

            var allMetadataParsed = int.TryParse(totalLinesStr, out var totalLines);
            allMetadataParsed = int.TryParse(linesFailedStr, out var linesFailed) && allMetadataParsed;
            allMetadataParsed = int.TryParse(filesCreatedStr, out var filesCreated) && allMetadataParsed;

            if (!allMetadataParsed)
            {
                // If can't parse, same as above, pretend nothing happened.
                return false;
            }

            ++totalStats.SourceFilesProcessed;
            totalStats.TotalLinesProcessed += totalLines;
            totalStats.TotalLinesFailed += linesFailed;
            totalStats.TotalFilesCreated += filesCreated;
            return true;
        }

        private static string GetBlobName(StorageListItem blob)
        {
            var path = blob.Uri.AbsoluteUri;
            var lastSlash = path.LastIndexOf('/');
            if (lastSlash < 0)
            {
                throw new ArgumentException($"Blob URI path does not contain '/': {blob.Uri.AbsolutePath}", nameof(blob));
            }

            return path.Substring(lastSlash + 1);
        }

        private async Task WriteReports(
            int instanceId,
            ConcurrentBag<LineProcessingContext> individualReports,
            BlobStatistics blobStats,
            string blobName,
            CancellationToken cancellationToken)
        {
            int numFailures = 0;
            int itemsProcessed = 0;
            _logger.LogInformation("Starting {InstanceId} instance of report writer", instanceId);
            var sw = Stopwatch.StartNew();
            while (individualReports.TryTake(out LineProcessingContext details))
            {
                PackageIdContainer data;
                try
                {
                    data = JsonConvert.DeserializeObject<PackageIdContainer>(details.Data);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Instance {InstanceId} failed to parse line {LineNumber} in {BlobName}: {Line}",
                        instanceId,
                        details.LineNumber,
                        blobName,
                        details.Data);
                    blobStats.IncrementLinesFailed();
                    ++numFailures;
                    continue;
                }
                var outFilename = $"recentpopularitydetail_{data.PackageId.ToLowerInvariant()}.json";
                var destinationUri = _destinationStorage.ResolveUri(outFilename);
                var storageContent = new StringStorageContent(details.Data, JsonContentType);

                await _destinationStorage.Save(destinationUri, storageContent, overwrite: true, cancellationToken: cancellationToken);
                blobStats.IncrementFileCreated();
                if (++itemsProcessed % 100 == 0)
                {
                    _logger.LogInformation("Processed {NumItems} items, got {NumFailures} failures to parse in {Elapsed} by {Instance}",
                        itemsProcessed,
                        numFailures,
                        sw.Elapsed,
                        instanceId);
                }
            }
            sw.Stop();
            _logger.LogInformation("Instance {Instance} done processing {BlobName}. Processed {NumItems} items, got {NumFailures} failures to parse in {Elapsed}",
                instanceId,
                blobName,
                itemsProcessed,
                numFailures,
                sw.Elapsed);
        }
    }
}

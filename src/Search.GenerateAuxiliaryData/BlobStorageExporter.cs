// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Search.GenerateAuxiliaryData
{
    /// <summary>
    /// Used for data that needs to be copied from a blob storage
    /// </summary>
    public class BlobStorageExporter : Exporter
    {
        private static readonly TimeSpan MaxCopyDuration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CopyPollFrequency = TimeSpan.FromMilliseconds(500);

        private CloudBlobContainer _sourceContainer;
        private string _sourceName;

        public BlobStorageExporter(ILogger<Exporter> logger, CloudBlobContainer sourceContainer, string sourceName, CloudBlobContainer destinationContainer, string destinationName):
            base(logger, destinationContainer, destinationName)
        {
            _sourceContainer = sourceContainer ?? throw new ArgumentNullException(nameof(sourceContainer));
            _sourceName = sourceName;
        }

        public override async Task ExportAsync()
        {
            _logger.LogInformation("Copying {ReportName} report from {ConnectionString}/{SourceName}", Name, _sourceContainer.Uri, _sourceName);

            await _destinationContainer.CreateIfNotExistsAsync();

            var sourceCloudBlob = _sourceContainer.GetBlockBlobReference(_sourceName);
            var destinationCloudBlob = _destinationContainer.GetBlockBlobReference(Name);


            await destinationCloudBlob.StartCopyAsync(sourceCloudBlob);

            var stopwatch = Stopwatch.StartNew();
            while (destinationCloudBlob.CopyState.Status == CopyStatus.Pending
                   && stopwatch.Elapsed < MaxCopyDuration)
            {
                await destinationCloudBlob.FetchAttributesAsync();
                await Task.Delay(CopyPollFrequency);
            }

            if (destinationCloudBlob.CopyState.Status == CopyStatus.Pending)
            {
                throw new TimeoutException($"Waiting for the blob copy operation to complete timed out after {MaxCopyDuration.TotalSeconds} seconds.");
            }
            else if (destinationCloudBlob.CopyState.Status != CopyStatus.Success)
            {
                throw new StorageException($"The blob copy operation had copy status {destinationCloudBlob.CopyState.Status} ({destinationCloudBlob.CopyState.StatusDescription}).");
            }

            _logger.LogInformation("Copy of {ReportName} completed. Took: {Seconds} seconds.", Name, stopwatch.Elapsed.TotalSeconds);
        }
    }
}

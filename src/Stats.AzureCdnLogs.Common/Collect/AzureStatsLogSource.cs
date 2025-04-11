// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using ICSharpCode.SharpZipLib.GZip;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Stats.AzureCdnLogs.Common.Collect
{
    /// <summary>
    /// A <see cref="ILogSource"/> implementation using Azure Storage as the storage. 
    /// </summary>
    public class AzureStatsLogSource : ILogSource
    {
        private const ushort GzipLeadBytes = 0x8b1f;

        /// <summary>
        ///  Maximum allowed lease time for Azure Blobs is 60 seconds.
        /// </summary>
        private const int CopyBlobLeaseTimeInSeconds = 60;

        private string _deadletterContainerName = "-deadletter";
        private string _archiveContainerName = "-archive";
        private BlobServiceClient _blobServiceClient;
        private AzureBlobLeaseManager _blobLeaseManager;
        private BlobContainerClient _container;
        private readonly ILogger<AzureStatsLogSource> _logger;

        /// <summary>
        /// .ctor for the AzureStatsLogSource
        /// </summary>
        /// <param name="connectionString">The connection string for the Azure account.</param>
        /// <param name="containerName">The container name.</param>
        public AzureStatsLogSource(BlobServiceClient blobServiceClient,
            string containerName,
            int azureServerTimeoutInSeconds,
            AzureBlobLeaseManager blobLeaseManager,
            ILogger<AzureStatsLogSource> logger)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrEmpty(containerName))
            {
                if (containerName == null)
                    throw new ArgumentNullException(nameof(containerName));
                else
                    throw new ArgumentException(nameof(containerName));
            }
            _container = _blobServiceClient.GetBlobContainerClient(containerName);

            _blobLeaseManager = blobLeaseManager ?? throw new ArgumentNullException(nameof(blobLeaseManager));
            _deadletterContainerName = $"{containerName}-deadletter";
            _archiveContainerName = $"{containerName}-archive";
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the Azure blobs that do not have a lease on them from the specific container .
        /// </summary>
        /// <param name="maxResults">The max number of files to be returned.</param>
        /// <param name="token">A token to be used for cancellation.</param>
        /// <returns></returns>
        public async Task<IEnumerable<Uri>> GetFilesAsync(int maxResults, CancellationToken token, string prefix = null)
        {
            if (maxResults <= 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(maxResults)} needs to be positive.");
            }

            _logger.LogInformation(
                "Finding up to {MaxResults} unlocked blobs with prefix {Prefix}...",
                maxResults,
                prefix);

            var result = new List<Uri>();
            await foreach (var blobItem in _container.GetBlobsAsync(prefix: prefix, cancellationToken: token))
            {

                if (result.Count >= maxResults)
                {
                    break;
                }

                _logger.LogInformation("Found blob {BlobName}, determining lease status...", blobItem.Name);

                BlobClient blob = _container.GetBlobClient(blobItem.Name);
                BlobProperties properties = await blob.GetPropertiesAsync(cancellationToken: token);
                if (properties.LeaseStatus != LeaseStatus.Unlocked)
                {
                    _logger.LogInformation(
                        "Skipping blob {BlobUrl} as its lease status is not unlocked: {LeaseStatus}",
                        blob.Uri,
                        properties.LeaseStatus);
                    continue;
                }

                result.Add(blob.Uri);
            }

            _logger.LogInformation(
                "Found {Results} unlocked blobs with prefix {Prefix}",
                result.Count,
                prefix);

            return result;
        }

        /// <summary>
        /// Open the blob from the specific Uri for read.
        /// </summary>
        /// <param name="blobUri">The blob uri.</param>
        /// <param name="contentType">A flag for the expected compression type of the blob.</param>
        /// <param name="token">A token to be used for cancellation.</param>
        /// <returns>The stream opened for read.</returns>
        public async Task<Stream> OpenReadAsync(Uri blobUri, ContentType contentType, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("OpenReadAsync: The operation was cancelled.");
                return null;
            }
            var blob = await GetBlobAsync(blobUri);
            if (blob == null)
            {
                _logger.LogInformation("OpenReadAsync: The blob was not found. Blob {BlobUri}", blobUri.AbsoluteUri);
                return null;
            }

            BlobProperties properties = await blob.GetPropertiesAsync();
            _logger.LogInformation("Opening blob {Filename}, {BlobSize} bytes", blob.Name, properties.ContentLength);
            var inputRawStream = await blob.OpenReadAsync();
            switch (contentType)
            {
                case ContentType.GZip:
                    return new GZipInputStream(inputRawStream);
                case ContentType.Text:
                case ContentType.None:
                    return inputRawStream;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentType));
            }
        }

        /// <summary>
        /// Take the lease on the blob. 
        /// The lease will be renewed at every 60 seconds. In order to stop the renew of the lease:
        /// 1. Call ReleaseLockAsync
        ///     or
        /// 2.Cancel the cancellation token
        /// </summary>
        /// <param name="blobUri">The blob uri.</param>
        /// <param name="token">A token to be used for cancellation.</param>
        /// <returns>True if the lock was taken. False otherwise. And the task that renews the lock overtime.</returns>
        public async Task<AzureBlobLockResult> TakeLockAsync(Uri blobUri, CancellationToken token)
        {
            var blob = await GetBlobAsync(blobUri);
            if (blob == null)
            {
                return AzureBlobLockResult.FailedLockResult(blob);
            }
            return await _blobLeaseManager.AcquireLease(blob, token);
        }

        /// <summary>
        /// Release the lock on the blob.
        /// </summary>
        /// <param name="blobLock">The blobLock as was taken at the beginning of the operation.</param>
        /// <param name="token">A token to be used for cancellation. For this implementation the token is ignored.</param>
        /// <returns>True if the lease was released or the blob does not exist.</returns>
        public async Task<AsyncOperationResult> TryReleaseLockAsync(AzureBlobLockResult blobLock, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("TryReleaseLockAsync: The operation was cancelled. BlobUri {BlobUri}.", blobLock.Blob.Uri);
                new AsyncOperationResult(false, new OperationCanceledException(token));
            }
            if (await blobLock.Blob.ExistsAsync())
            {
                return await _blobLeaseManager.TryReleaseLockAsync(blobLock);
            }
            return new AsyncOperationResult(false, null);
        }

        /// <summary>
        /// It will perform the clean up steps.
        /// In this case it will copy the blob in the archive or dead-letter and delete the blob.
        /// This method will not throw an exception. 
        /// Use the <see cref="AsyncOperationResult.OperationException"/> for any exception during this execution.
        /// </summary>
        /// <param name="blobLock">The <see cref="AzureBlobLockResult"/> for the blob that needs clean-up.</param>
        /// <param name="onError">Flag to indicate if the cleanup is done because an error or not. </param>
        /// <param name="token">A token to be used for cancellation.</param>
        /// <returns>True is the cleanup was successful. If the blob does not exist the return value is false.</returns>
        public async Task<AsyncOperationResult> TryCleanAsync(AzureBlobLockResult blobLock, bool onError, CancellationToken token)
        {
            _logger.LogInformation("CleanAsync:Start cleanup for {Blob}", blobLock.Blob.Uri);
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("CleanAsync: The operation was cancelled. BlobUri {BlobUri}.", blobLock.Blob.Uri);
                new AsyncOperationResult(false, new OperationCanceledException(token));
            }
            try
            {
                var sourceBlob = await GetBlobAsync(blobLock.Blob.Uri);
                if (sourceBlob == null)
                {
                    _logger.LogError("CleanAsync: The blob {Blob} was not found.", blobLock.Blob.Uri);
                    return new AsyncOperationResult(false, null);
                }
                string archiveTargetContainerName = onError ? _deadletterContainerName : _archiveContainerName;
                _logger.LogInformation("CleanAsync: Blob {Blob} will be copied to container {Container}", blobLock.Blob.Uri, archiveTargetContainerName);
                var archiveTargetContainer = await CreateContainerAsync(archiveTargetContainerName);
                if (await CopyBlobToContainerAsync(blobLock, archiveTargetContainer, token))
                {
                    _logger.LogInformation("CleanAsync: Blob {Blob} was copied to container {Container}", blobLock.Blob.Uri, archiveTargetContainerName);
                    var blobRequestConditions = new BlobRequestConditions { LeaseId = blobLock.LeaseId };
                    // The operation will throw if the lease does not match
                    bool deleteResult = await sourceBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots,
                        conditions: blobRequestConditions,
                        cancellationToken: token);
                    _logger.LogInformation("CleanAsync: Blob {Blob} was deleted {DeletedResult}. The leaseId: {LeaseId}", blobLock.Blob.Uri, deleteResult, blobLock.LeaseId);
                    return new AsyncOperationResult(deleteResult, null);
                }
                _logger.LogWarning("CleanAsync: Blob {Blob} failed to be copied to container {Container}", blobLock.Blob.Uri, archiveTargetContainerName);
                return new AsyncOperationResult(false, null);
            }
            catch (Exception exception)
            {
                _logger.LogCritical(LogEvents.FailedBlobDelete, exception, "CleanAsync: Blob {Blob} failed the clean-up. The current leaseId: {LeaseId}", blobLock.Blob.Uri, blobLock.LeaseId);
                return new AsyncOperationResult(null, exception);
            }
        }

        private async Task<BlobClient> GetBlobAsync(Uri blobUri)
        {
            try
            {
                var blobUriBuilder = new BlobUriBuilder(blobUri);
                var containerClient = _blobServiceClient.GetBlobContainerClient(blobUriBuilder.BlobContainerName);
                var _blobClient = containerClient.GetBlobClient(blobUriBuilder.BlobName);
                var properties = await _blobClient.GetPropertiesAsync();
                return _blobClient;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Copy the blob from source to the destination 
        /// </summary>
        /// <param name="sourceBlobInformation">The source blobLock as was taken at the beginning of the operation.</param>
        /// <param name="destinationContainer">The destination Container.</param>
        /// <returns></returns>
        private async Task<bool> CopyBlobToContainerAsync(AzureBlobLockResult sourceBlobInformation, BlobContainerClient destinationContainer, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("CopyBlobToContainerAsync: The operation was cancelled.");
                return false;
            }

            //just get a reference to the future blob
            var destinationBlob = destinationContainer.GetBlobClient(GetBlobNameFromUri(sourceBlobInformation.Blob.Uri));
            try
            {
                if (!await destinationBlob.ExistsAsync(token))
                {
                    return await TryCopyInternalAsync(sourceBlobInformation.Blob, destinationBlob, destinationContainer);
                }
                else
                {
                    _logger.LogInformation("CopyBlobToContainerAsync: Blob already exists DestinationUri {DestinationUri}.", destinationBlob.Uri);

                    // Overwrite
                    var leaseClient = destinationBlob.GetBlobLeaseClient();
                    var leaseResponse = await leaseClient.AcquireAsync(TimeSpan.FromSeconds(CopyBlobLeaseTimeInSeconds), cancellationToken: token);
                    var destinationAccessCondition = new BlobRequestConditions { LeaseId = leaseResponse.Value.LeaseId };
                    await destinationBlob.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots, conditions: destinationAccessCondition, cancellationToken: token);
                    var result = await TryCopyInternalAsync(sourceBlobInformation.Blob, destinationBlob, destinationContainer);
                    try
                    {
                        await leaseClient.ReleaseAsync(destinationAccessCondition);
                    }
                    catch (Azure.RequestFailedException)
                    {
                        // do not do anything the lease will be released anyway
                    }
                    return result;
                }
            }
            catch (Azure.RequestFailedException exception)
            {
                _logger.LogCritical(LogEvents.FailedBlobCopy, exception, "CopyBlobToContainerAsync: Blob Copy Failed. SourceUri: {SourceUri}. DestinationUri {DestinationUri}", sourceBlobInformation.Blob.Uri, destinationBlob.Uri);
                return false;
            }
        }

        private async Task<bool> TryCopyInternalAsync(BlobClient sourceBlob,
            BlobClient destinationBlob,
            BlobContainerClient destinationContainer)
        {
            var copySourceBlobUri = sourceBlob.Uri;

            await destinationBlob.StartCopyFromUriAsync(copySourceBlobUri);

            //round-trip to the server and get the information 
            destinationBlob = destinationContainer.GetBlobClient(GetBlobNameFromUri(sourceBlob.Uri));

            BlobProperties properties = await destinationBlob.GetPropertiesAsync();

            while (properties.CopyStatus == CopyStatus.Pending)
            {
                properties = await destinationBlob.GetPropertiesAsync();
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            }

            BlobProperties resultProperties = await destinationBlob.GetPropertiesAsync();
            return resultProperties.CopyStatus == CopyStatus.Success; ;
        }


        private string GetBlobNameFromUri(Uri blobUri)
        {
            return blobUri.Segments.LastOrDefault();
        }

        private async Task<BlobContainerClient> CreateContainerAsync(string containerName)
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        private static async Task<bool> IsGzipCompressedAsync(Stream stream)
        {
            stream.Position = 0;
            try
            {
                var bytes = new byte[4];
                await stream.ReadAsync(bytes, 0, 4);
                return BitConverter.ToUInt16(bytes, 0) == GzipLeadBytes;
            }
            finally
            {
                stream.Position = 0;
            }
        }
    }
}

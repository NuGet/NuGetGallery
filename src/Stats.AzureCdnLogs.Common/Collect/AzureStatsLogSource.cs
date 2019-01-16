// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using ICSharpCode.SharpZipLib.GZip;

namespace Stats.AzureCdnLogs.Common.Collect
{
    /// <summary>
    /// A <see cref="ILogSource"/> implementation using Azure Storage as the storage. 
    /// </summary>
    public class AzureStatsLogSource : ILogSource
    {
        private const ushort GzipLeadBytes = 0x8b1f;

        private string _deadletterContainerName = "-deadletter";
        private string _archiveContainerName = "-archive";
        private CloudStorageAccount _azureAccount;
        private AzureBlobLeaseManager _blobLeaseManager;
        private CloudBlobContainer _container;
        private CloudBlobClient _blobClient;
        private BlobRequestOptions _blobRequestOptions;
        private readonly ILogger<AzureStatsLogSource> _logger;

        /// <summary>
        /// .ctor for the AzureStatsLogSource
        /// </summary>
        /// <param name="connectionString">The connection string for the Azure account.</param>
        /// <param name="containerName">The container name.</param>
        public AzureStatsLogSource(CloudStorageAccount storageAccount,
            string containerName,
            int azureServerTimeoutInSeconds,
            AzureBlobLeaseManager blobLeaseManager,
            ILogger<AzureStatsLogSource> logger)
        {
            _azureAccount = storageAccount;
            _blobClient = _azureAccount.CreateCloudBlobClient();
            _container = _blobClient.GetContainerReference(containerName);
            _blobRequestOptions = new BlobRequestOptions();
            _blobRequestOptions.ServerTimeout = TimeSpan.FromSeconds(azureServerTimeoutInSeconds);
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
        public async Task<IEnumerable<Uri>> GetFilesAsync(int maxResults, CancellationToken token)
        {
            if (maxResults<= 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(maxResults)} needs to be positive.");
            }
            BlobContinuationToken continuationToken = null;
            var result = new List<Uri>();
            do
            {
                var resultsInternal = await _container.ListBlobsSegmentedAsync(prefix: null,
                    useFlatBlobListing: true,
                    blobListingDetails: BlobListingDetails.Metadata,
                    maxResults: null,
                    currentToken: continuationToken,
                    options: _blobRequestOptions,
                    operationContext: null,
                    cancellationToken: token);

                result.AddRange(resultsInternal.Results.Where((r) =>
                {
                    var cloudBlob = (CloudBlob)r;
                    cloudBlob.FetchAttributes();
                    return cloudBlob.Properties.LeaseStatus == LeaseStatus.Unlocked;
                }).Select(r => r.Uri));
                if(result.Count > maxResults)
                {
                    break;
                }
            }
            while (continuationToken != null);
            return result.Take(maxResults);
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
            if(token.IsCancellationRequested)
            {
                _logger.LogInformation("OpenReadAsync: The operation was cancelled.");
                return null;
            }
            var blob = await GetBlobAsync(blobUri);
            if(blob == null)
            {
                _logger.LogInformation("OpenReadAsync: The blob was not found. Blob {BlobUri}", blobUri.AbsoluteUri);
                return null;
            }
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
        public async Task<Tuple<bool, Task>> TakeLockAsync(Uri blobUri, CancellationToken token)
        {
            var blob = await GetBlobAsync(blobUri);
            if (blob == null)
            {
                return new Tuple<bool, Task>(false, null);
            }
            Task<bool> renewStateTask;
            return new Tuple<bool,Task>(_blobLeaseManager.AcquireLease(blob, token, out renewStateTask), renewStateTask);
        }

        /// <summary>
        /// Release the lock on the blob.
        /// </summary>
        /// <param name="blobUri">The blob uri to be released.</param>
        /// <param name="token">A token to be used for cancellation. For this implemention the token is ignored.</param>
        /// <returns>True if the lease was released or the blob does not exist.</returns>
        public async Task<bool> ReleaseLockAsync(Uri blobUri, CancellationToken token)
        {
            var blob = await GetBlobAsync(blobUri);
            if (blob != null)
            {
                bool tryReleaseLease = _blobLeaseManager.TryReleaseLease(blob);
                _logger.LogInformation("ReleaseLockAsync: ReleaseLeaseStatus: {LeaseReleased} on the {BlobUri}.", tryReleaseLease, blobUri.AbsoluteUri);
                return tryReleaseLease;
            }
            return true;
        }

        /// <summary>
        /// It will perform the clean up steps.
        /// In this case it will copy the blob in the archive or dead-letter and delete the blob.
        /// </summary>
        /// <param name="blobUri">The blob uri to be cleaned.</param>
        /// <param name="onError">Flag to indicate if the cleanup is done because an error or not. </param>
        /// <param name="token">A token to be used for cancellation.</param>
        /// <returns>True is the cleanup was sucessful. If the blob does not exist the return value is false.</returns>
        public async Task<bool> CleanAsync(Uri blobUri, bool onError, CancellationToken token)
        {
            _logger.LogInformation("CleanAsync:Start cleanup for {Blob}", blobUri.AbsoluteUri);
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("CleanAsync: The operation was cancelled.");
                return false;
            }

            var sourceBlob = await GetBlobAsync(blobUri);
            if(sourceBlob == null)
            {
                _logger.LogError("CleanAsync: The blob {Blob} was not found.", blobUri.AbsoluteUri);
                return false;
            }
            string archiveTargetContainerName = onError ? _deadletterContainerName : _archiveContainerName;
            _logger.LogInformation("CleanAsync: Blob {Blob} will be copied to container {Container}", blobUri.AbsoluteUri, archiveTargetContainerName);
            var archiveTargetContainer = await CreateContainerAsync(archiveTargetContainerName);
            if (await CopyBlobToContainerAsync(blobUri, archiveTargetContainer))
            {
                _logger.LogInformation("CleanAsync: Blob {Blob} was copied to container {Container}", blobUri.AbsoluteUri, archiveTargetContainerName);
                string leaseId = string.Empty;
                AccessCondition acc = _blobLeaseManager.HasLease(sourceBlob, out leaseId) ? new AccessCondition() { LeaseId = leaseId } : null;
                bool deleteResult = await sourceBlob.DeleteIfExistsAsync(deleteSnapshotsOption: DeleteSnapshotsOption.IncludeSnapshots, accessCondition: acc, options: _blobRequestOptions, operationContext: null);
                _logger.LogInformation("CleanAsync: Blob {Blob} was deleted {DeletedResult}. The leaseId: {LeaseId}", blobUri.AbsoluteUri, deleteResult, leaseId);
                return deleteResult;
            }
            _logger.LogWarning("CleanAsync: Blob {Blob} failed to be copied to container {Container}", blobUri.AbsoluteUri, archiveTargetContainerName);
            return false;
        }

        private async Task<CloudBlob> GetBlobAsync(Uri blobUri)
        {
            try
            {
                var blob = await _blobClient.GetBlobReferenceFromServerAsync(blobUri, accessCondition: null, options: _blobRequestOptions, operationContext: null);
                return blob as CloudBlob;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<bool> CopyBlobToContainerAsync(Uri sourceblobUri, CloudBlobContainer destcontainer)
        {
            //just get a reference to the future blob
            var destinationBlob = destcontainer.GetBlobReference(GetBlobNameFromUri(sourceblobUri));
            if (!await destinationBlob.ExistsAsync())
            {
                await destinationBlob.StartCopyAsync(sourceblobUri);

                //round-trip to the server and get the information 
                destinationBlob = (CloudBlob)destcontainer.GetBlobReferenceFromServer(GetBlobNameFromUri(sourceblobUri));

                while (destinationBlob.CopyState.Status == CopyStatus.Pending)
                {
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                    destinationBlob = (CloudBlob)destcontainer.GetBlobReference(GetBlobNameFromUri(sourceblobUri));
                }
                return true;
            }
            return false;
        }

        private string GetBlobNameFromUri(Uri blobUri)
        {
            return blobUri.Segments.LastOrDefault();
        }

        private async Task<CloudBlobContainer> CreateContainerAsync(string containerName)
        {
            var container = _blobClient.GetContainerReference(containerName);
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

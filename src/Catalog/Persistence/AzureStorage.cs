// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using NuGet.Protocol;
using NuGet.Services.Metadata.Catalog.Extensions;
using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorage : Storage, IAzureStorage
    {
        private readonly bool _compressContent;
        private readonly IThrottle _throttle;
        private readonly ICloudBlobDirectory _directory;
        private readonly IBlobContainerClientWrapper _blobContainerClientWrapper;
        private readonly bool _useServerSideCopy;

        public const string Sha512HashAlgorithmId = "SHA512";
        public static readonly TimeSpan DefaultServerTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan DefaultMaxExecutionTime = TimeSpan.FromMinutes(10);

        public AzureStorage(
            BlobServiceClient blobServiceClient,
            string containerName,
            string path,
            Uri baseAddress,
            TimeSpan maxExecutionTime,
            TimeSpan serverTimeout,
            bool useServerSideCopy,
            bool compressContent,
            bool verbose,
            bool initializeContainer,
            IThrottle throttle) : this(
                new CloudBlobDirectoryWrapper(blobServiceClient, containerName, path),
                baseAddress,
                maxExecutionTime,
                serverTimeout,
                initializeContainer)
        {
            _useServerSideCopy = useServerSideCopy;
            _compressContent = compressContent;
            _throttle = throttle ?? NullThrottle.Instance;
            Verbose = verbose;
        }

        public AzureStorage(
            Uri storageBaseUri,
            TimeSpan maxExecutionTime,
            TimeSpan serverTimeout,
            bool useServerSideCopy,
            bool compressContent,
            bool verbose,
            IThrottle throttle)
            : this(GetCloudBlobDirectoryUri(storageBaseUri), storageBaseUri, maxExecutionTime, serverTimeout, initializeContainer: false)
        {
            _useServerSideCopy = useServerSideCopy;
            _compressContent = compressContent;
            _throttle = throttle ?? NullThrottle.Instance;
            Verbose = verbose;
        }

        private static ICloudBlobDirectory GetCloudBlobDirectoryUri(Uri storageBaseUri)
        {
            if (storageBaseUri.AbsoluteUri.Contains('%'))
            {
                // Later in the code for the sake of simplicity wrong things are done with URL that 
                // can explode when URL is specially crafted with certain URL-encoded characters.
                // Since it is URL for our storage root where we know that we don't use anything
                // that requires URL-encoding, we'll just throw here just in case, to keep code
                // below simple.
                throw new ArgumentException("Storage URL cannot contain URL-encoded characters");
            }

            var pathSegments = storageBaseUri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length < 1)
            {
                throw new ArgumentException("Storage URL must contain some path");
            }

            var blobEndpoint = new Uri(storageBaseUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped));
            // Create BlobServiceClient with anonymous credentials
            var blobServiceClient = new BlobServiceClient(blobEndpoint, new AzureSasCredential(""));

            string containerName = pathSegments[0];
            // Get a reference to a container
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            string pathInContainer = string.Join("/", pathSegments.Skip(1));
            return new CloudBlobDirectoryWrapper(blobServiceClient, containerName, pathInContainer);
        }

        public AzureStorage(
            ICloudBlobDirectory directory,
            Uri baseAddress,
            TimeSpan maxExecutionTime,
            TimeSpan serverTimeout,
            bool initializeContainer) : base(
                baseAddress ?? GetDirectoryUri(directory))
        {
            // Unless overridden at the level of a single API call, these options will apply to all service calls that 
            // use BlobRequestOptions.

            var blobClientOptions = new BlobClientOptions
            {
                Retry =
                {
                    Mode = Azure.Core.RetryMode.Exponential,
                    Delay = new TimeSpan(serverTimeout.Ticks / 2), // Initial delay, similar to ExponentialRetry behavior
                    MaxDelay = maxExecutionTime, // Maximum delay between retries
                    NetworkTimeout = serverTimeout // Equivalent to ServerTimeout
                }
            };

            _directory = new CloudBlobDirectoryWrapper(directory.ServiceClient, directory.ContainerClientWrapper.ContainerClient.Name, directory.DirectoryPrefix, blobClientOptions);
            _blobContainerClientWrapper = _directory.ContainerClientWrapper;

            if (initializeContainer)
            {
                BlobContainerInfo blobContainerInfo = _blobContainerClientWrapper.ContainerClient.CreateIfNotExists(PublicAccessType.Blob);

                if (blobContainerInfo != null && Verbose)
                {
                    Trace.WriteLine($"Created '{_blobContainerClientWrapper.ContainerClient.Name}' public container");
                }
            }
        }

        public override async Task<OptimisticConcurrencyControlToken> GetOptimisticConcurrencyControlTokenAsync(
            Uri resourceUri,
            CancellationToken cancellationToken)
        {
            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            cancellationToken.ThrowIfCancellationRequested();

            string blobName = GetName(resourceUri);
            BlockBlobClient blockBlobClient = GetBlockBlobReference(blobName);

            BlobProperties properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return new OptimisticConcurrencyControlToken(properties.ETag.ToString());
        }

        private static Uri GetDirectoryUri(ICloudBlobDirectory directory)
        {
            Uri uri = new UriBuilder(directory.Uri)
            {
                Scheme = "http",
                Port = 80
            }.Uri;

            return uri;
        }

        // Blob exists
        public override bool Exists(string fileName)
        {
            Uri packageRegistrationUri = ResolveUri(fileName);
            string blobName = GetName(packageRegistrationUri);

            BlockBlobClient blockBlobClient = GetBlockBlobReference(blobName);

            if (blockBlobClient.Exists())
            {
                return true;
            }

            if (Verbose)
            {
                Trace.WriteLine(string.Format("The blob {0} does not exist.", packageRegistrationUri));
            }
            return false;
        }

        public override async Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken)
        {
            var blobs = new List<StorageListItem>();

            BlobContainerClient blobContainerClient = _blobContainerClientWrapper.ContainerClient;
            await foreach (var blobItem in blobContainerClient.GetBlobsAsync(prefix: _directory.DirectoryPrefix, cancellationToken: cancellationToken))
            {
                var lastModified = blobItem.Properties.LastModified?.UtcDateTime;
                blobs.Add(new StorageListItem(new Uri(blobContainerClient.Uri, blobItem.Name), lastModified));
            }

            return blobs;
        }

        public override async Task<bool> UpdateCacheControlAsync(Uri resourceUri, string cacheControl, CancellationToken cancellationToken)
        {
            string blobName = GetName(resourceUri);
            BlockBlobClient blockBlobClient = GetBlockBlobReference(blobName);

            BlobProperties properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            if (properties.CacheControl != cacheControl)
            {
                var headers = new BlobHttpHeaders { CacheControl = cacheControl };
                await blockBlobClient.SetHttpHeadersAsync(headers, cancellationToken: cancellationToken);
                return true;
            }

            return false;
        }

        protected override async Task OnCopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellationToken)
        {
            var azureDestinationStorage = destinationStorage as AzureStorage;

            if (azureDestinationStorage == null)
            {
                throw new NotImplementedException("Copying is only supported from Azure storage to Azure storage.");
            }

            string sourceName = GetName(sourceUri);
            string destinationName = azureDestinationStorage.GetName(destinationUri);

            BlockBlobClient sourceBlockBlob = GetBlockBlobReference(sourceName);
            BlockBlobClient destinationBlockBlob = azureDestinationStorage.GetBlockBlobReference(destinationName);

            // Start the copy operation
            CopyFromUriOperation copyOperation = await destinationBlockBlob.StartCopyFromUriAsync(sourceBlockBlob.Uri, cancellationToken: cancellationToken);

            // Wait for the copy operation to complete
            await copyOperation.WaitForCompletionAsync(cancellationToken);

            // Set destination properties if provided
            if (destinationProperties?.Count > 0)
            {
                BlobHttpHeaders headers = new BlobHttpHeaders();
                
                // The copy statement copied all properties from the source blob to the destination blob; however,
                // there may be required properties on destination blob, all of which may have not already existed
                // on the source blob at the time of copy.
                foreach (KeyValuePair<string, string> property in destinationProperties)
                {
                    switch (property.Key)
                    {
                        case StorageConstants.CacheControl:
                            headers.CacheControl = property.Value;
                            break;

                        case StorageConstants.ContentType:
                            headers.ContentType = property.Value;
                            break;

                        default:
                            throw new NotImplementedException($"Storage property '{property.Key}' is not supported.");
                    }
                }

                await destinationBlockBlob.SetHttpHeadersAsync(headers, cancellationToken: cancellationToken);
            }
        }

        protected override async Task OnSaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            string blobName = GetName(resourceUri);
            BlockBlobClient blockBlobClient = GetBlockBlobReference(blobName);

            var headers = new BlobHttpHeaders
            {
                ContentType = content.ContentType,
                CacheControl = content.CacheControl
            };

            if (_compressContent)
            {
                headers.ContentEncoding = "gzip";
                using (Stream stream = content.GetContentStream())
                {
                    MemoryStream destinationStream = new MemoryStream();

                    using (GZipStream compressionStream = new GZipStream(destinationStream, CompressionMode.Compress, true))
                    {
                        await stream.CopyToAsync(compressionStream);
                    }

                    destinationStream.Seek(0, SeekOrigin.Begin);

                    IAccessCondition accessCondition = (content as StringStorageContentWithAccessCondition)?.AccessCondition;
                    BlobRequestConditions blobRequestConditions = accessCondition.ToBlobRequestConditions();

                    await blockBlobClient.UploadAsync(destinationStream, headers, conditions: blobRequestConditions, cancellationToken: cancellationToken);

                    Trace.WriteLine($"Saved compressed blob {blockBlobClient.Uri} to container {_blobContainerClientWrapper.ContainerClient.Name}");
                }
            }
            else
            {
                using (Stream stream = content.GetContentStream())
                {
                    await blockBlobClient.UploadAsync(stream, headers, cancellationToken: cancellationToken);

                    Trace.WriteLine($"Saved uncompressed blob {blockBlobClient.Uri} to container {_blobContainerClientWrapper.ContainerClient.Name}");
                }
            }

            await TryTakeBlobSnapshotAsync(blockBlobClient);
        }

        /// <summary>
        /// Take one snapshot only if there is not any snapshot for the specific blob
        /// This will prevent the blob to be deleted by a not intended delete action
        /// </summary>
        /// <param name="blob"></param>
        /// <returns></returns>
        private async Task<bool> TryTakeBlobSnapshotAsync(BlockBlobClient blobBlockClient)
        {
            if (blobBlockClient == null)
            {
                return false;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (_blobContainerClientWrapper.HasOnlyOriginalSnapshot(blobBlockClient.Name))
                {
                    var response = await blobBlockClient.CreateSnapshotAsync();
                    stopwatch.Stop();
                    Trace.WriteLine($"SnapshotCreated:milliseconds={stopwatch.ElapsedMilliseconds}:{blobBlockClient.Uri.ToString()}:{response?.Value.Snapshot}");
                }
                return true;
            }
            catch (RequestFailedException e)
            {
                stopwatch.Stop();
                Trace.WriteLine($"EXCEPTION:milliseconds={stopwatch.ElapsedMilliseconds}:CreateSnapshot: Failed to take the snapshot for blob {blobBlockClient.Uri.ToString()}. Exception{e.ToString()}");
                return false;
            }
        }

        protected override async Task<StorageContent> OnLoadAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            // the Azure SDK will treat a starting / as an absolute URL,
            // while we may be working in a subdirectory of a storage container
            // trim the starting slash to treat it as a relative path
            string blobName = GetName(resourceUri).TrimStart('/');
            BlockBlobClient blobClient = GetBlockBlobReference(blobName);

            await _throttle.WaitAsync();
            try
            {
                Response<BlobProperties> properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

                string content;
                using (var originalStream = new MemoryStream())
                {
                    await blobClient.DownloadToAsync(originalStream, cancellationToken);

                    originalStream.Seek(0, SeekOrigin.Begin);

                    if (properties.Value.ContentEncoding == "gzip")
                    {
                        using (var uncompressedStream = new GZipStream(originalStream, CompressionMode.Decompress))
                        {
                            using (var reader = new StreamReader(uncompressedStream))
                            {
                                content = await reader.ReadToEndAsync();
                            }
                        }
                    }
                    else
                    {
                        using (var reader = new StreamReader(originalStream))
                        {
                            content = await reader.ReadToEndAsync();
                        }
                    }
                }

                return new StringStorageContentWithETag(content, properties.Value.ETag.ToString());
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                if (Verbose)
                {
                    Trace.WriteLine($"Can't load '{resourceUri}'. Blob doesn't exist");
                }

                return null;
            }
            finally
            {
                _throttle.Release();
            }
        }

        protected override async Task OnDeleteAsync(Uri resourceUri, DeleteRequestOptions deleteRequestOptions, CancellationToken cancellationToken)
        {
            string blobName = GetName(resourceUri);
            BlobRequestConditions accessCondition = (deleteRequestOptions as DeleteRequestOptionsWithAccessCondition)?.BlobRequestConditions;
            BlockBlobClient blobClient = GetBlockBlobReference(blobName);
            await blobClient.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots, accessCondition, cancellationToken);
        }

        public override Uri GetUri(string name)
        {
            var baseUri = _directory.Uri.AbsoluteUri;

            if (baseUri.EndsWith("/"))
            {
                return new Uri($"{baseUri}{name}", UriKind.Absolute);
            }

            return new Uri($"{baseUri}/{name}", UriKind.Absolute);
        }

        public override async Task<bool> AreSynchronized(Uri firstResourceUri, Uri secondResourceUri)
        {
            BlockBlobClient sourceBlobClient = new BlockBlobClient(firstResourceUri);
            BlockBlobClient destinationBlobClient = GetBlockBlobReference(GetName(secondResourceUri));

            // For interacting with the source, we just use the same blob request options as the destination blob.
            return await AreSynchronized(new AzureCloudBlockBlob(sourceBlobClient), new AzureCloudBlockBlob(destinationBlobClient));
        }

        public async Task<bool> AreSynchronized(ICloudBlockBlob sourceBlockBlob, ICloudBlockBlob destinationBlockBlob)
        {
            if (await destinationBlockBlob.ExistsAsync(CancellationToken.None))
            {
                if (await sourceBlockBlob.ExistsAsync(CancellationToken.None))
                {
                    var sourceBlobMetadata = await sourceBlockBlob.GetMetadataAsync(CancellationToken.None);
                    var destinationBlobMetadata = await destinationBlockBlob.GetMetadataAsync(CancellationToken.None);
                    if (sourceBlobMetadata == null || destinationBlobMetadata == null)
                    {
                        return false;
                    }

                    var sourceBlobHasSha512Hash = sourceBlobMetadata.TryGetValue(Sha512HashAlgorithmId, out var sourceBlobSha512Hash);
                    var destinationBlobHasSha512Hash = destinationBlobMetadata.TryGetValue(Sha512HashAlgorithmId, out var destinationBlobSha512Hash);
                    if (!sourceBlobHasSha512Hash)
                    {
                        Trace.TraceWarning($"The source blob ({sourceBlockBlob.Uri}) doesn't have the SHA512 hash.");
                    }
                    if (!destinationBlobHasSha512Hash)
                    {
                        Trace.TraceWarning($"The destination blob ({destinationBlockBlob.Uri}) doesn't have the SHA512 hash.");
                    }
                    if (sourceBlobHasSha512Hash && destinationBlobHasSha512Hash)
                    {
                        if (sourceBlobSha512Hash == destinationBlobSha512Hash)
                        {
                            Trace.WriteLine($"The source blob ({sourceBlockBlob.Uri}) and destination blob ({destinationBlockBlob.Uri}) have the same SHA512 hash and are synchronized.");
                            return true;
                        }

                        // The SHA512 hash between the source and destination blob should be always same.
                        Trace.TraceWarning($"The source blob ({sourceBlockBlob.Uri}) and destination blob ({destinationBlockBlob.Uri}) have the different SHA512 hash and are not synchronized. " +
                            $"The source blob hash is {sourceBlobSha512Hash} while the destination blob hash is {destinationBlobSha512Hash}");
                    }

                    return false;
                }
                return true;
            }
            return !(await sourceBlockBlob.ExistsAsync(CancellationToken.None));
        }

        public async Task<ICloudBlockBlob> GetCloudBlockBlobReferenceAsync(Uri blobUri)
        {
            string blobName = GetName(blobUri);
            BlockBlobClient blockBlobClient = GetBlockBlobReference(blobName);
            bool blobExists = await blockBlobClient.ExistsAsync();

            if (Verbose && !blobExists)
            {
                Trace.WriteLine($"The blob {blobUri.AbsoluteUri} does not exist.");
            }

            return new AzureCloudBlockBlob(blockBlobClient);
        }

        public async Task<bool> HasPropertiesAsync(Uri blobUri, string contentType, string cacheControl)
        {
            string blobName = GetName(blobUri);
            BlockBlobClient blobClient = GetBlockBlobReference(blobName);

            if (await blobClient.ExistsAsync())
            {
                var properties = await blobClient.GetPropertiesAsync();

                return string.Equals(properties.Value.ContentType, contentType)
                    && string.Equals(properties.Value.CacheControl, cacheControl);
            }

            return false;
        }

        private BlockBlobClient GetBlockBlobReference(string blobName)
        {
            IBlobContainerClientWrapper containerClient = _directory.ContainerClientWrapper;
            BlockBlobClient blobClient = containerClient.GetBlockBlobClient(blobName);

            // ApplyBlobRequestOptions(blobClient) is not needed as the options should be set at the client level
            // when creating the BlobServiceClient or BlobContainerClient.

            return blobClient;
        }
    }
}

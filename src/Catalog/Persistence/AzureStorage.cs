using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NuGet.Protocol;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorage : Storage, IAzureStorage
    {
        private readonly bool _compressContent;
        private readonly IThrottle _throttle;
        private readonly BlobContainerClient _containerClient;
        private readonly bool _useServerSideCopy;

        public const string Sha512HashAlgorithmId = "SHA512";
        public static readonly TimeSpan DefaultServerTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan DefaultMaxExecutionTime = TimeSpan.FromMinutes(10);

        public AzureStorage(
            BlobContainerClient containerClient,
            string path,
            Uri baseAddress,
            TimeSpan maxExecutionTime,
            TimeSpan serverTimeout,
            bool useServerSideCopy,
            bool compressContent,
            bool verbose,
            bool initializeContainer,
            IThrottle throttle) : base(baseAddress)
        {
            _containerClient = containerClient;
            _useServerSideCopy = useServerSideCopy;
            _compressContent = compressContent;
            _throttle = throttle ?? NullThrottle.Instance;
            Verbose = verbose;

            if (initializeContainer)
            {
                _containerClient.CreateIfNotExists();
                if (Verbose)
                {
                    Trace.WriteLine($"Created '{_containerClient.Name}' public container");
                }
            }
        }

        public bool CompressContent { get; }

        public override Storage Create(string name = null)
        {
            string path = name;

            Uri newBase = BaseAddress;

            if (newBase != null && !string.IsNullOrEmpty(name))
            {
                newBase = new Uri(BaseAddress, name + "/");
            }

            return new AzureStorage(
                _containerClient,
                path,
                newBase,
                DefaultMaxExecutionTime,
                DefaultServerTimeout,
                _useServerSideCopy,
                CompressContent,
                Verbose,
                initializeContainer: false,
                _throttle);
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
            BlobClient blobClient = _containerClient.GetBlobClient(blobName);

            BlobProperties properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            return new OptimisticConcurrencyControlToken(properties.ETag.ToString());
        }

        // Blob exists
        public override bool Exists(string fileName)
        {
            Uri packageRegistrationUri = ResolveUri(fileName);
            string blobName = GetName(packageRegistrationUri);

            BlobClient blobClient = _containerClient.GetBlobClient(blobName);

            if (blobClient.Exists())
            {
                return true;
            }
            if (Verbose)
            {
                Trace.WriteLine($"The blob {packageRegistrationUri} does not exist.");
            }
            return false;
        }

        public override async Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken)
        {
            var blobs = _containerClient.GetBlobsAsync(cancellationToken: cancellationToken);

            var files = new List<StorageListItem>();

            await foreach (var blobItem in blobs)
            {
                files.Add(new StorageListItem(new Uri(_containerClient.Uri, blobItem.Name), blobItem.Properties.LastModified?.UtcDateTime));
            }

            return files;
        }

        public override async Task<bool> UpdateCacheControlAsync(Uri resourceUri, string cacheControl, CancellationToken cancellationToken)
        {
            string blobName = GetName(resourceUri);
            BlobClient blobClient = _containerClient.GetBlobClient(blobName);

            BlobProperties properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            if (properties.CacheControl != cacheControl)
            {
                BlobHttpHeaders headers = new BlobHttpHeaders
                {
                    CacheControl = cacheControl
                };

                await blobClient.SetHttpHeadersAsync(headers, cancellationToken: cancellationToken);
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
            if (destinationStorage is not AzureStorage azureDestinationStorage)
            {
                throw new NotImplementedException("Copying is only supported from Azure storage to Azure storage.");
            }

            string sourceName = GetName(sourceUri);
            string destinationName = azureDestinationStorage.GetName(destinationUri);

            BlobClient sourceBlobClient = _containerClient.GetBlobClient(sourceName);
            BlobClient destinationBlobClient = azureDestinationStorage._containerClient.GetBlobClient(destinationName);

            await destinationBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri, cancellationToken: cancellationToken);

            if (destinationProperties?.Count > 0)
            {
                BlobProperties properties = await destinationBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                BlobHttpHeaders headers = new BlobHttpHeaders();

                foreach (var property in destinationProperties)
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
                            throw new NotImplementedException($"Storage property '{property.Value}' is not supported.");
                    }
                }

                await destinationBlobClient.SetHttpHeadersAsync(headers, cancellationToken: cancellationToken);
            }
        }

        protected override async Task OnSaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            BlobClient blobClient = _containerClient.GetBlobClient(name);

            BlobHttpHeaders headers = new BlobHttpHeaders
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

                    await blobClient.UploadAsync(destinationStream, headers, cancellationToken: cancellationToken);

                    Trace.WriteLine($"Saved compressed blob {blobClient.Uri} to container {_containerClient.Name}");
                }
            }
            else
            {
                using (Stream stream = content.GetContentStream())
                {
                    await blobClient.UploadAsync(stream, headers, cancellationToken: cancellationToken);
                }

                Trace.WriteLine($"Saved uncompressed blob {blobClient.Uri} to container {_containerClient.Name}");
            }
        }

        protected override async Task<StorageContent> OnLoadAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri).TrimStart('/');

            BlobClient blobClient = _containerClient.GetBlobClient(name);

            await _throttle.WaitAsync();
            try
            {
                string content;

                using (var originalStream = new MemoryStream())
                {
                    await blobClient.DownloadToAsync(originalStream, cancellationToken: cancellationToken);

                    originalStream.Seek(0, SeekOrigin.Begin);

                    if (blobClient.GetProperties().Value.ContentEncoding == "gzip")
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

                return new StringStorageContentWithETag(content, blobClient.GetProperties().Value.ETag.ToString());
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
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
            string name = GetName(resourceUri);

            var accessCondition = (deleteRequestOptions as DeleteRequestOptionsWithAccessCondition)?.AccessCondition;

            BlobClient blobClient = _containerClient.GetBlobClient(name);
            await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, conditions: accessCondition, cancellationToken: cancellationToken);
        }

        public override Uri GetUri(string name)
        {
            var baseUri = _containerClient.Uri.AbsoluteUri;

            if (baseUri.EndsWith("/"))
            {
                return new Uri($"{baseUri}{name}", UriKind.Absolute);
            }

            return new Uri($"{baseUri}/{name}", UriKind.Absolute);
        }

        public override async Task<bool> AreSynchronized(Uri firstResourceUri, Uri secondResourceUri)
        {
            var sourceBlobClient = _containerClient.GetBlobClient(firstResourceUri.AbsolutePath.TrimStart('/'));
            var destinationBlobClient = _containerClient.GetBlobClient(secondResourceUri.AbsolutePath.TrimStart('/'));

            return await AreSynchronized(sourceBlobClient, destinationBlobClient);
        }

        public async Task<bool> AreSynchronized(BlobClient sourceBlobClient, BlobClient destinationBlobClient)
        {
            if (await destinationBlobClient.ExistsAsync())
            {
                if (await sourceBlobClient.ExistsAsync())
                {
                    var sourceBlobProperties = await sourceBlobClient.GetPropertiesAsync();
                    var destinationBlobProperties = await destinationBlobClient.GetPropertiesAsync();

                    if (sourceBlobProperties.Value.Metadata.TryGetValue(Sha512HashAlgorithmId, out var sourceBlobSha512Hash) &&
                        destinationBlobProperties.Value.Metadata.TryGetValue(Sha512HashAlgorithmId, out var destinationBlobSha512Hash))
                    {
                        if (sourceBlobSha512Hash == destinationBlobSha512Hash)
                        {
                            Trace.WriteLine($"The source blob ({sourceBlobClient.Uri}) and destination blob ({destinationBlobClient.Uri}) have the same SHA512 hash and are synchronized.");
                            return true;
                        }

                        Trace.TraceWarning($"The source blob ({sourceBlobClient.Uri}) and destination blob ({destinationBlobClient.Uri}) have different SHA512 hashes and are not synchronized. The source blob hash is {sourceBlobSha512Hash} while the destination blob hash is {destinationBlobSha512Hash}.");
                    }

                    return false;
                }
                return true;
            }
            return !(await sourceBlobClient.ExistsAsync());
        }

        public async Task<BlobClient> GetBlobClientReferenceAsync(Uri blobUri)
        {
            string blobName = GetName(blobUri);
            BlobClient blobClient = _containerClient.GetBlobClient(blobName);
            var blobExists = await blobClient.ExistsAsync();

            if (Verbose && !blobExists)
            {
                Trace.WriteLine($"The blob {blobUri.AbsoluteUri} does not exist.");
            }

            return blobClient;
        }

        public async Task<bool> HasPropertiesAsync(Uri blobUri, string contentType, string cacheControl)
        {
            var blobName = GetName(blobUri);
            var blobClient = _containerClient.GetBlobClient(blobName);

            if (await blobClient.ExistsAsync())
            {
                var properties = await blobClient.GetPropertiesAsync();

                return string.Equals(properties.Value.ContentType, contentType)
                    && string.Equals(properties.Value.CacheControl, cacheControl);
            }

            return false;
        }
    }
}

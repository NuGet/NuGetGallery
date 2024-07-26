// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.DataMovement;
using Azure.Storage.DataMovement.Blobs;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Storage
{
    public class AzureStorage : Storage
    {
        private readonly ILogger<AzureStorage> _logger;
        private readonly BlobContainerClient _directory;
        private readonly TransferManager _transferManager;
        private readonly BlobsStorageResourceProvider _storageResourceProvider;
        private readonly bool _useServerSideCopy;
        private readonly string _path;

        public AzureStorage(
            BlobServiceClient account,
            BlobsStorageResourceProvider blobsStorageResourceProvider,
            string containerName,
            string path,
            Uri baseAddress,
            bool useServerSideCopy,
            bool initializeContainer,
            ILogger<AzureStorage> logger)
            : this(
                  account.GetBlobContainerClient(containerName),
                  blobsStorageResourceProvider,
                  baseAddress,
                  useServerSideCopy,
                  initializeContainer,
                  logger)
        {
            _path = path;
        }

        private AzureStorage(
            BlobContainerClient directory,
            BlobsStorageResourceProvider blobsStorageResourceProvider,
            Uri baseAddress,
            bool useServerSideCopy,
            bool initializeContainer,
            ILogger<AzureStorage> logger)
            : base(baseAddress ?? GetDirectoryUri(directory), logger)
        {
            _logger = logger;
            _directory = directory;
            _useServerSideCopy = useServerSideCopy;
            var checkpointerOptions = new TransferCheckpointStoreOptions(Path.GetTempPath());
            var transferManagerOptions = new TransferManagerOptions();
            transferManagerOptions.CheckpointerOptions = checkpointerOptions;
            _transferManager = new TransferManager(transferManagerOptions);
            _storageResourceProvider = blobsStorageResourceProvider;

            if (initializeContainer)
            {
                var storageActionResult = _directory.CreateIfNotExists(PublicAccessType.Blob);

                if (Verbose)
                {
                    if (storageActionResult != null)
                    {
                        _logger.LogInformation("Created {ContainerName} publish container", _directory.Name);
                    }
                    else
                    {
                        _logger.LogInformation("Container {ContainerName} already existed. Create was no-oped", _directory.Name);
                    }
                }
            }

            ResetStatistics();
        }

        public bool CompressContent
        {
            get;
            set;
        }

        static Uri GetDirectoryUri(BlobContainerClient directory)
        {
            Uri uri = new UriBuilder(directory.Uri)
            {
                Scheme = "http",
                Port = 80
            }.Uri;

            return uri;
        }

        //Blob exists
        public override bool Exists(string fileName)
        {
            Uri packageRegistrationUri = ResolvePathedUri(fileName);
            string blobName = GetName(packageRegistrationUri);

            BlockBlobClient blob = _directory.GetBlockBlobClient(blobName);

            if (blob.Exists())
            {
                return true;
            }
            if (Verbose)
            {
                _logger.LogInformation("The blob {BlobUri} does not exist.", packageRegistrationUri);
            }
            return false;
        }

        public override async Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken)
        {
            Uri packageRegistrationUri = ResolvePathedUri(fileName);
            string blobName = GetName(packageRegistrationUri);

            BlockBlobClient blob = _directory.GetBlockBlobClient(blobName);

            if (await blob.ExistsAsync(cancellationToken))
            {
                return true;
            }
            if (Verbose)
            {
                _logger.LogInformation("The blob {BlobUri} does not exist.", packageRegistrationUri);
            }
            return false;
        }

        public override IEnumerable<StorageListItem> List(bool getMetadata)
        {
            var blobTraits = new BlobTraits();
            if (getMetadata)
            {
                blobTraits |= BlobTraits.Metadata;
            }

            foreach (BlobHierarchyItem blob in _directory.GetBlobsByHierarchy(traits: blobTraits, prefix: _path))
            {
                yield return GetStorageListItem(_directory.GetBlockBlobClient(blob.Blob.Name));
            }
        }

        public override async Task<IEnumerable<StorageListItem>> ListAsync(bool getMetadata, CancellationToken cancellationToken)
        {
            var blobTraits = new BlobTraits();
            if (getMetadata)
            {
                blobTraits |= BlobTraits.Metadata;
            }

            var blobList = new List<StorageListItem>();

            await foreach (BlobHierarchyItem blob in _directory.GetBlobsByHierarchyAsync(traits: blobTraits, prefix: _path))
            {
                blobList.Add(await GetStorageListItemAsync(_directory.GetBlockBlobClient(blob.Blob.Name)));
            }

            return blobList;
        }

        public override async Task SetMetadataAsync(Uri resourceUri, IDictionary<string, string> metadata)
        {
            BlockBlobClient blob = GetBlobReference(GetName(resourceUri));
            await blob.SetMetadataAsync(metadata);
        }

        private async Task<StorageListItem> GetStorageListItemAsync(BlockBlobClient listBlobItem)
        {
            var blobPropertiesResponse = await listBlobItem.GetPropertiesAsync();
            var blobProperties = blobPropertiesResponse?.Value;
            var lastModified = blobProperties?.LastModified;

            return new StorageListItem(listBlobItem.Uri, lastModified, blobProperties?.Metadata);
        }

        private StorageListItem GetStorageListItem(BlockBlobClient listBlobItem)
        {
            var blobPropertiesResponse = listBlobItem.GetProperties();
            var blobProperties = blobPropertiesResponse?.Value;
            var lastModified = blobProperties?.LastModified;

            return new StorageListItem(listBlobItem.Uri, lastModified, blobProperties?.Metadata);
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

            var destinationOptions = new AppendBlobStorageResourceOptions();

            if (destinationProperties?.Count > 0)
            {
                foreach (KeyValuePair<string,string> property in destinationProperties)
                {
                    switch (property.Key)
                    {
                        case StorageConstants.CacheControl:
                            destinationOptions.HttpHeaders.CacheControl = property.Value;
                            break;

                        case StorageConstants.ContentType:
                            destinationOptions.HttpHeaders.ContentType = property.Value;
                            break;

                        default:
                            throw new NotImplementedException($"Storage property '{property.Value}' is not supported.");
                    }
                }
            }

            StorageResource sourceStorageResource = _storageResourceProvider.FromBlob(sourceUri.ToString());
            StorageResource destinationStorageResource = _storageResourceProvider.FromBlob(destinationUri.ToString(), destinationOptions);

            var transferOptions = new DataTransferOptions();

            DataTransfer transfer = await _transferManager.StartTransferAsync(sourceStorageResource, destinationStorageResource, transferOptions: transferOptions, cancellationToken: cancellationToken);
            await transfer.WaitForCompletionAsync();
        }

        //  save
        protected override async Task OnSave(Uri resourceUri, StorageContent content, bool overwrite, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            BlockBlobClient blob = _directory.GetBlockBlobClient(name);
            BlobHttpHeaders headers = new BlobHttpHeaders();
            headers.ContentType = content.ContentType;
            headers.CacheControl = content.CacheControl;

            if (!overwrite && await blob.ExistsAsync())
            {
                _logger.LogWarning("Blob existed and overwrite was not specified. Blob Name: {BlobName}", blob.Name);
                return;
            }

            if (CompressContent)
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

                    await blob.UploadAsync(
                        destinationStream,
                        options: null,
                        cancellationToken: cancellationToken);

                    await blob.SetHttpHeadersAsync(headers);

                    if (Verbose)
                    {
                        _logger.LogInformation("Saved compressed blob {BlobUri} to container {ContainerName}", blob.Uri.ToString(), _directory.Name);
                    }
                }
            }
            else
            {
                using (Stream stream = content.GetContentStream())
                {
                    await blob.SetHttpHeadersAsync(headers);
                    await blob.UploadAsync(
                        stream,
                        options: null,
                        cancellationToken: cancellationToken);

                    if (Verbose)
                    {
                        _logger.LogInformation("Saved uncompressed blob {BlobUri} to container {ContainerName}", blob.Uri.ToString(), _directory.Name);
                    }
                }
            }
        }

        //  load
        protected override async Task<StorageContent> OnLoad(Uri resourceUri, CancellationToken cancellationToken)
        {
            // the Azure SDK will treat a starting / as an absolute URL,
            // while we may be working in a subdirectory of a storage container
            // trim the starting slash to treat it as a relative path
            string name = GetName(resourceUri).TrimStart('/');

            BlockBlobClient blob = _directory.GetBlockBlobClient(name);

            if (blob.Exists())
            {
                MemoryStream originalStream = new MemoryStream();
                await blob.DownloadToAsync(originalStream, cancellationToken);

                originalStream.Position = 0;
                var propertiesResponse = await blob.GetPropertiesAsync();
                var properties = propertiesResponse.Value;
                
                MemoryStream content;

                if (properties.ContentEncoding == "gzip")
                {
                    using (var uncompressedStream = new GZipStream(originalStream, CompressionMode.Decompress))
                    {
                        content = new MemoryStream();

                        await uncompressedStream.CopyToAsync(content);
                    }

                    content.Position = 0;
                }
                else
                {
                    content = originalStream;
                }

                return new StreamStorageContent(content);
            }

            if (Verbose)
            {
                _logger.LogInformation("Can't load {BlobUri}. Blob doesn't exist", resourceUri);
            }

            return null;
        }

        //  delete
        protected override async Task OnDelete(Uri resourceUri, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            BlockBlobClient blob = _directory.GetBlockBlobClient(name);

            await blob.DeleteAsync(cancellationToken: cancellationToken);
        }

        private BlockBlobClient GetBlobReference(string blobName)
        {
            var blob = _directory.GetBlockBlobClient(blobName);

            // The BlobClient should inherit the properties of the containerClient
            // This means that we no longer need to explicitly apply the default options like we used to.

            return blob;
        }

        // This method is a helper to insert the _path into the resolved Uri for the file so we get the correct full path.
        private Uri ResolvePathedUri(string filename)
        {
            return ResolveUri(Path.Combine(_path, filename));
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using NuGet.Services.Storage;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class CloudBlobDirectoryWrapper : ICloudBlobDirectory
    {
        private readonly IBlobServiceClientFactory _blobServiceClientFactory;
        private readonly BlobContainerClient _containerClient;
        private readonly string _directoryPrefix;
        private readonly IBlobContainerClientWrapper _blobContainerClientWrapper;
        private readonly BlobClientOptions _defaultClientOptions;

        public IBlobServiceClientFactory ServiceClient => new SimpleBlobServiceClientFactory(_containerClient.GetParentBlobServiceClient());
        public Uri Uri { get; }
        public string DirectoryPrefix => _directoryPrefix;
        public BlobClientOptions ContainerOptions => _defaultClientOptions;
        public IBlobContainerClientWrapper ContainerClientWrapper => _blobContainerClientWrapper;

        public CloudBlobDirectoryWrapper(IBlobServiceClientFactory serviceClient, string containerName, string directoryPrefix, BlobClientOptions blobClientOptions = null)
        {
            _blobServiceClientFactory = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
            _directoryPrefix = directoryPrefix ?? throw new ArgumentNullException(nameof(directoryPrefix));

            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            _defaultClientOptions = blobClientOptions ?? new BlobClientOptions();

            // Create the container client using the provided or default options
            if (blobClientOptions != null)
            {
                // Request a new BlobServiceClient instance with the new options
                var newServiceClient = _blobServiceClientFactory.GetBlobServiceClient(blobClientOptions);
                _containerClient = newServiceClient.GetBlobContainerClient(containerName);
            }
            else
            {
                _containerClient = _blobServiceClientFactory.GetBlobServiceClient(blobClientOptions: null).GetBlobContainerClient(containerName);
            }

            Uri = new Uri(Storage.RemoveQueryString(_containerClient.Uri).TrimEnd('/') + "/" + _directoryPrefix);

            _blobContainerClientWrapper = new BlobContainerClientWrapper(_containerClient);
        }

        public BlockBlobClient GetBlockBlobClient(string blobName)
        {
            return _containerClient.GetBlockBlobClient(_directoryPrefix + blobName);
        }

        // Assuming we'll use BlobHierarchyItem hierarchy items (with a virtual directory structure) over BlobItem(flat blobs)
        public async Task<IEnumerable<BlobHierarchyItem>> ListBlobsAsync(CancellationToken cancellationToken)
        {
            var items = new List<BlobHierarchyItem>();
            var resultSegment = _containerClient.GetBlobsByHierarchyAsync(prefix: _directoryPrefix).AsPages();

            await foreach (Azure.Page<BlobHierarchyItem> blobPage in resultSegment.WithCancellation(cancellationToken))
            {
                foreach (BlobHierarchyItem blobItem in blobPage.Values)
                {
                    items.Add(blobItem);
                }
            }

            return items;
        }
    }
}

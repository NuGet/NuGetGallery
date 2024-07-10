// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class CloudBlobDirectoryWrapper : ICloudBlobDirectory
    {
        private readonly BlobContainerClient _containerClient;
        private readonly string _directoryPrefix;
        private readonly IBlobContainerClientWrapper _blobContainerClientWrapper;
        private readonly BlobClientOptions _defaultClientOptions;

        public BlobServiceClient ServiceClient => _containerClient.GetParentBlobServiceClient();
        public Uri Uri => new Uri(_containerClient.Uri, _directoryPrefix);
        public string DirectoryPrefix => _directoryPrefix;
        public BlobClientOptions ContainerOptions => _defaultClientOptions;
        public IBlobContainerClientWrapper ContainerClientWrapper => _blobContainerClientWrapper;

        public CloudBlobDirectoryWrapper(BlobServiceClient serviceClient, string containerName, string directoryPrefix, BlobClientOptions  blobClientOptions = null)
        {
            _directoryPrefix = directoryPrefix ?? throw new ArgumentNullException(nameof(directoryPrefix));
            _defaultClientOptions = new BlobClientOptions();
            _containerClient = serviceClient.GetBlobContainerClient(containerName) ?? throw new ArgumentNullException(nameof(containerName));

            if (blobClientOptions != null)
            {
                _defaultClientOptions = blobClientOptions;
                // Extract necessary information
                Uri serviceUri = _containerClient.Uri;
                // Create a new BlobServiceClient instance with the new options, we couldn't change options for existing instance.
                _containerClient = new BlobContainerClient(serviceUri, blobClientOptions);
            }

            _blobContainerClientWrapper = new BlobContainerClientWrapper(_containerClient);
        }

        public BlockBlobClient GetBlockBlobClient(string blobName)
        {
            return _containerClient.GetBlockBlobClient(_directoryPrefix + blobName);
        }

        // Assuming we'll use BlobHierarchyItem hierarchy items (with a virtual directory structure) over BlobItem(flat blobs)
        public async Task<IEnumerable<BlobHierarchyItem>> ListBlobsAsync(CancellationToken cancellationToken)
        {
            return await _containerClient.ListBlobsAsync(_directoryPrefix, cancellationToken);
        }
    }
}

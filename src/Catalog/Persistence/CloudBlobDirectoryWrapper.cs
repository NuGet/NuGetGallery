// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System.Diagnostics;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class CloudBlobDirectoryWrapper : ICloudBlobDirectory
    {
        private readonly BlobContainerClient _containerClient;
        private readonly string _directoryPrefix;
        private readonly IBlobContainerClientWrapper _blobContainerClientWrapper;
        private readonly BlobClientOptions _defaultClientOptions;

        public BlobServiceClient ServiceClient => _containerClient.GetParentBlobServiceClient();
        public Uri Uri { get; }
        public string DirectoryPrefix => _directoryPrefix;
        public BlobClientOptions ContainerOptions => _defaultClientOptions;
        public IBlobContainerClientWrapper ContainerClientWrapper => _blobContainerClientWrapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudBlobDirectoryWrapper"/> class using a BlobServiceClient.
        /// </summary>
        /// <param name="serviceClient">The BlobServiceClient to use.</param>
        /// <param name="containerName">The name of the blob container.</param>
        /// <param name="directoryPrefix">The directory prefix within the container.</param>
        /// <param name="blobClientOptions">Optional blob client options.</param>
        public CloudBlobDirectoryWrapper(BlobServiceClient serviceClient, string containerName, string directoryPrefix, BlobClientOptions blobClientOptions = null)
        {
            _directoryPrefix = directoryPrefix ?? throw new ArgumentNullException(nameof(directoryPrefix));

            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            _defaultClientOptions = blobClientOptions ?? new BlobClientOptions();
            var _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
            
            // Create the container client using the provided or default options
            if (blobClientOptions != null)
            {
                // Just use the service client with the container name
                // The options should already be applied to the service client
                _containerClient = _serviceClient.GetBlobContainerClient(containerName);
            }
            else
            {
                _containerClient = _serviceClient.GetBlobContainerClient(containerName);
            }

            Uri = new Uri(Storage.RemoveQueryString(_containerClient.Uri).TrimEnd('/') + "/" + _directoryPrefix);

            _blobContainerClientWrapper = new BlobContainerClientWrapper(_containerClient);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudBlobDirectoryWrapper"/> class using a connection string.
        /// </summary>
        /// <param name="connectionString">The connection string to use for authentication.</param>
        /// <param name="containerName">The name of the blob container.</param>
        /// <param name="directoryPrefix">The directory prefix within the container.</param>
        /// <param name="blobClientOptions">Optional blob client options.</param>
        public CloudBlobDirectoryWrapper(string connectionString, string containerName, string directoryPrefix, BlobClientOptions blobClientOptions = null)
        {
            _directoryPrefix = directoryPrefix ?? throw new ArgumentNullException(nameof(directoryPrefix));

            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            _defaultClientOptions = blobClientOptions ?? new BlobClientOptions();
            
            // Create service client and container client from connection string
            var serviceClient = new BlobServiceClient(connectionString, _defaultClientOptions);
            _containerClient = serviceClient.GetBlobContainerClient(containerName);
            
            Uri = new Uri(Storage.RemoveQueryString(_containerClient.Uri).TrimEnd('/') + "/" + _directoryPrefix);

            _blobContainerClientWrapper = new BlobContainerClientWrapper(_containerClient);
        }

        /// <summary>
        /// Gets a BlobServiceClient with DefaultAzureCredential (kept for backward compatibility).
        /// </summary>
        /// <param name="uri">Storage service URI.</param>
        /// <param name="blobClientOptions">Optional blob client options.</param>
        /// <returns>A BlobServiceClient instance.</returns>
        public static BlobServiceClient GetBlobServiceClient(Uri uri, BlobClientOptions blobClientOptions = null)
        {
            return GetBlobServiceClient(uri, null, blobClientOptions);
        }

        /// <summary>
        /// Gets a BlobServiceClient using various authentication methods.
        /// </summary>
        /// <param name="uri">Storage service URI.</param>
        /// <param name="connectionString">Optional connection string for authentication.</param>
        /// <param name="blobClientOptions">Optional blob client options.</param>
        /// <returns>A BlobServiceClient instance.</returns>
        public static BlobServiceClient GetBlobServiceClient(Uri uri, string connectionString = null, BlobClientOptions blobClientOptions = null)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            // First try to use connection string if provided
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return new BlobServiceClient(connectionString, blobClientOptions);
            }
            
            // If no connection string, try DefaultAzureCredential
            try
            {
                var credential = new DefaultAzureCredential();
                return new BlobServiceClient(uri, credential, blobClientOptions);
            }
            catch (Exception ex)
            {
                // If DefaultAzureCredential fails, log it but continue with AnonymousCredential
                // as a fallback for public storage accounts
                Trace.WriteLine($"Failed to authenticate with DefaultAzureCredential: {ex.Message}");
                
                // For public containers, try anonymous access
                return new BlobServiceClient(uri, blobClientOptions);
            }
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

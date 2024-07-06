// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class CloudBlobDirectoryWrapper : ICloudBlobDirectory
    {
        private readonly BlobContainerClient _containerClient;
        private readonly string _directoryPath;

        public BlobServiceClient ServiceClient => _containerClient.GetParentBlobServiceClient();
        public BlobContainerClient Container => _containerClient;
        public Uri Uri => new Uri(_containerClient.Uri, _directoryPath);

        public CloudBlobDirectoryWrapper(BlobServiceClient serviceClient, string containerName, string directoryPath)
        {
            _containerClient = serviceClient.GetBlobContainerClient(containerName) ?? throw new ArgumentNullException(nameof(containerName));
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            _directoryPath = directoryPath?.Trim('/') + '/';
        }

        public BlobClient GetBlobClient(string blobName)
        {
            return _containerClient.GetBlobClient(_directoryPath + blobName);
        }

        public async Task<IEnumerable<BlobHierarchyItem>> ListBlobsAsync(CancellationToken cancellationToken)
        {
            return await _containerClient.ListBlobsAsync(_directoryPath, cancellationToken);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class BlobContainerClientWrapper : IBlobContainerClientWrapper
    {
        private readonly BlobContainerClient _containerClient;

        public BlobContainerClientWrapper(BlobContainerClient containerClient)
        {
            _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        }

        public BlobContainerClient ContainerClient => _containerClient;

        public BlockBlobClient GetBlockBlobClient(string blobName)
        {
            return _containerClient.GetBlockBlobClient(blobName);
        }

        public Uri GetUri()
        {
            return _containerClient.Uri;
        }

        public async Task<bool> HasNoSnapshotAsync(Uri resourceUri, string blobName, CancellationToken cancellationToken)
        {
            var blobItems = _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.Snapshots, prefix: blobName, cancellationToken);

            await foreach (BlobItem blobItem in blobItems)
            {
                if (!string.IsNullOrEmpty(blobItem.Snapshot))
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<IList<Snapshot>> ListSnapshotsAsync(Uri resourceUri, string blobName, CancellationToken cancellationToken)
        {
            var blobItems = _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.Snapshots, prefix: blobName, cancellationToken);

            IList<Snapshot> snapshots = new List<Snapshot>();
            await foreach (BlobItem blobItem in blobItems)
            {
                if (!string.IsNullOrEmpty(blobItem.Snapshot))
                {
                    snapshots.Add(new BlobSnapshot(resourceUri, blobItem.Snapshot));
                }
            }

            return snapshots;
        }
    }
}

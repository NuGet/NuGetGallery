// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
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

        public bool HasOnlyOriginalSnapshot(string prefix)
        {
            var blobs = _containerClient.GetBlobs(
                    BlobTraits.None,
                    states: BlobStates.Snapshots,
                    prefix: prefix);

            return blobs.Count() == 1;
        }
    }
}

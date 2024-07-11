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
        private readonly BlobContainerClient _client;

        public BlobContainerClientWrapper(BlobContainerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public BlockBlobClient GetBlockBlobClient(string blobName)
        {
            return _client.GetBlockBlobClient(blobName);
        }

        public Uri GetUri()
        {
            return _client.Uri;
        }

        public bool HasOnlyOriginalSnapshot(string prefix)
        {
            var blobs = _client.GetBlobs(
                    BlobTraits.None,
                    states: BlobStates.Snapshots,
                    prefix: prefix);

            return blobs.Count() == 1;
        }
    }
}

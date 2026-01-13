// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface IBlobContainerClientWrapper
    {
        BlobContainerClient ContainerClient { get; }
        BlockBlobClient GetBlockBlobClient(string blobName);
        Uri GetUri();
        Task<bool> HasNoSnapshotAsync(Uri resourceUri, string blobName, CancellationToken cancellationToken);
        Task<IList<Snapshot>> ListSnapshotsAsync(Uri resourceUri, string blobName, CancellationToken cancellationToken);
    }
}

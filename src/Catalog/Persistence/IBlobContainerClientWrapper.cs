// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface IBlobContainerClientWrapper
    {
        BlobContainerClient ContainerClient { get; }
        BlockBlobClient GetBlockBlobClient(string blobName);
        Uri GetUri();
        bool HasOnlyOriginalSnapshot(string prefix);
    }
}

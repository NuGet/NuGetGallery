// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using NuGet.Services.Storage;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface ICloudBlobDirectory
    {
        IBlobServiceClientFactory ServiceClient { get; }
        IBlobContainerClientWrapper ContainerClientWrapper { get; }
        string DirectoryPrefix { get; }
        Uri Uri { get; }

        BlockBlobClient GetBlockBlobClient(string blobName);
        Task<IEnumerable<BlobHierarchyItem>> ListBlobsAsync(CancellationToken cancellationToken);
    }
}

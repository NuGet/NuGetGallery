// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface ICloudBlobDirectory
    {
        BlobServiceClient ServiceClient { get; }
        BlobContainerClient Container { get; }
        Uri Uri { get; }

        BlobClient GetBlobClient(string blobName);
        Task<IEnumerable<BlobHierarchyItem>> ListBlobsAsync(CancellationToken cancellationToken);
    }
}

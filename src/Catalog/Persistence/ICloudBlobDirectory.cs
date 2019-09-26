// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface ICloudBlobDirectory
    {
        ICloudBlockBlobClient ServiceClient { get; }
        CloudBlobContainer Container { get; }
        Uri Uri { get; }

        CloudBlockBlob GetBlockBlobReference(string blobName);
        Task<IEnumerable<IListBlobItem>> ListBlobsAsync(CancellationToken cancellationToken);
    }
}
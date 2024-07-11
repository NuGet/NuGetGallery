// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface ICloudBlockBlob
    {
        Task<string> GetContentMD5Async(CancellationToken cancellationToken);
        Task<string> GetETagAsync(CancellationToken cancellationToken);
        Task<long> GetLengthAsync(CancellationToken cancellationToken);
        Uri Uri { get; }

        Task<bool> ExistsAsync(CancellationToken cancellationToken);
        Task<BlobProperties> FetchAttributesAsync(CancellationToken cancellationToken);
        Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken);
        Task<Stream> GetStreamAsync(CancellationToken cancellationToken);
    }
}
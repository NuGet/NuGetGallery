// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface ICloudBlockBlob
    {
        string ContentMD5 { get; set; }
        string ETag { get; }
        long Length { get; }
        Uri Uri { get; }

        Task<bool> ExistsAsync(CancellationToken cancellationToken);
        Task FetchAttributesAsync(CancellationToken cancellationToken);
        Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken);
        Task<Stream> GetStreamAsync(CancellationToken cancellationToken);
        Task SetPropertiesAsync(AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext);
    }
}
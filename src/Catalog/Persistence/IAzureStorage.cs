// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface IAzureStorage : IStorage
    {
        Task<ICloudBlockBlob> GetCloudBlockBlobReferenceAsync(Uri uri);
        Task<bool> HasPropertiesAsync(Uri blobUri, string contentType, string cacheControl);
    }
}
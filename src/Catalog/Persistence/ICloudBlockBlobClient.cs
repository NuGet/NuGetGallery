// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface ICloudBlockBlobClient
    {
        BlobClientOptions ClientOptions { get; set; }
    }
}

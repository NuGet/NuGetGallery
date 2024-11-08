// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;

namespace NuGet.Services.Storage
{
    public interface IBlobServiceClientFactory
    {
        public Uri Uri { get; }

        public BlobServiceClient GetBlobServiceClient(BlobClientOptions blobClientOptions);
    }
}

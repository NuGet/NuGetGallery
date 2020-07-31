// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class CloudBlockBlobClientWrapper : ICloudBlockBlobClient
    {
        private readonly CloudBlobClient _blobClient;

        public BlobRequestOptions DefaultRequestOptions
        {
            get => _blobClient.DefaultRequestOptions;
            set => _blobClient.DefaultRequestOptions = value;
        }

        public CloudBlockBlobClientWrapper(CloudBlobClient blobClient)
        {
            _blobClient = blobClient;
        }
    }
}

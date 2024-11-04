// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace NuGet.Services.Storage
{
    // This class is a simple wrapper around a BlobServiceClient to enable backwards compatibility
    // and avoid use cases where we would create unnecessary instances of BlobServiceClient
    public class SimpleBlobServiceClientFactory : IBlobServiceClientFactory
    {
        private BlobServiceClient _blobServiceClient;

        public Uri Uri => _blobServiceClient.Uri;

        public SimpleBlobServiceClientFactory(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        }

        public BlobServiceClient GetBlobServiceClient(BlobClientOptions blobClientOptions = null)
        {
            return _blobServiceClient;
        }
    }
}

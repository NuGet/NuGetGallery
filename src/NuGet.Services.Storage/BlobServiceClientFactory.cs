// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace NuGet.Services.Storage
{
    public class BlobServiceClientFactory : IBlobServiceClientFactory
    {
        private bool _useTokenCredential = false;
        private TokenCredential _credential;
        private string _connectionString = "";

        public virtual Uri Uri { get; set; }

        public BlobServiceClientFactory() { }

        public BlobServiceClientFactory(string connectionString)
        {
            _connectionString = connectionString;
            this.Uri = new BlobServiceClient(connectionString).Uri;
        }

        public BlobServiceClientFactory(Uri serviceUri, TokenCredential credential)
        {
            this.Uri = serviceUri;
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        }

        public BlobServiceClient GetBlobServiceClient(BlobClientOptions blobClientOptions = null)
        {
            if (_useTokenCredential)
            {
                return new BlobServiceClient(this.Uri, _credential, blobClientOptions);
            }
            else
            {
                return new BlobServiceClient(_connectionString, blobClientOptions);
            }
        }
    }
}

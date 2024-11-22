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
        private readonly BlobServiceClientAuthType? _authType;
        private TokenCredential _credential;
        private string _connectionString = "";

        public virtual Uri Uri { get; set; }

        protected BlobServiceClientFactory() { }

        public BlobServiceClientFactory(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            _connectionString = connectionString;
            this.Uri = new BlobServiceClient(connectionString).Uri;
            _authType = BlobServiceClientAuthType.ConnectionString;
        }

        public BlobServiceClientFactory(Uri serviceUri, TokenCredential credential = null)
        {
            this.Uri = serviceUri ?? throw new ArgumentNullException(nameof(serviceUri));

            if (credential != null)
            {
                _credential = credential;
                _authType = BlobServiceClientAuthType.TokenCredential;
            }
            else
            {
                _authType = BlobServiceClientAuthType.Anonymous;
            }
        }

        public virtual BlobServiceClient GetBlobServiceClient(BlobClientOptions blobClientOptions = null)
        {
            if (_authType.HasValue)
            {
                switch (_authType)
                {
                    case BlobServiceClientAuthType.TokenCredential:
                        return new BlobServiceClient(this.Uri, _credential, blobClientOptions);
                    case BlobServiceClientAuthType.ConnectionString:
                        return new BlobServiceClient(_connectionString, blobClientOptions);
                    case BlobServiceClientAuthType.Anonymous:
                        return new BlobServiceClient(this.Uri, blobClientOptions);
                }
            }

            throw new Exception("No authentication type configured");
        }
    }
}

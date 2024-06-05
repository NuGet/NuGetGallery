// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private readonly string _storageConnectionString;
        private readonly bool _readAccessGeoRedundant = false;
        private readonly Lazy<Uri> _primaryServiceUri;
        private readonly Lazy<Uri> _grsServiceUri;
        private BlobServiceClient _blobClient;
        private TokenCredential _tokenCredential;

        public CloudBlobClientWrapper(string storageConnectionString, bool readAccessGeoRedundant)
            : this()
        {
            _storageConnectionString = storageConnectionString;
            _readAccessGeoRedundant = readAccessGeoRedundant;
        }

        public CloudBlobClientWrapper(string storageConnectionString)
            : this()
        {
            _storageConnectionString = storageConnectionString;
        }

        private CloudBlobClientWrapper()
        {
            _primaryServiceUri = new Lazy<Uri>(GetPrimaryUri);
            _grsServiceUri = new Lazy<Uri>(GetGrsUri);
        }

        public static CloudBlobClientWrapper UsingMsi(string storageConnectionString, string clientId = null)
        {
            var client = new CloudBlobClientWrapper(storageConnectionString);
            client._tokenCredential = new ManagedIdentityCredential(clientId);
            return client;
        }

        public ISimpleCloudBlob GetBlobFromUri(Uri uri)
        {
            // For Azure blobs, the query string is assumed to be the SAS token.
            ISimpleCloudBlob blob;
            if (!string.IsNullOrEmpty(uri.Query))
            {
                var uriBuilder = new UriBuilder(uri);
                uriBuilder.Query = string.Empty;

                blob = new CloudBlobWrapper(new BlockBlobClient(
                    uriBuilder.Uri,
                    new AzureSasCredential(uri.Query)));
            }
            else
            {
                // TODO: do we need authentication here?
                blob = new CloudBlobWrapper(new BlockBlobClient(uri));
            }

            return blob;
        }

        public ICloudBlobContainer GetContainerReference(string containerAddress)
        {
            if (_blobClient == null)
            {
                var options = new BlobClientOptions();
                if (_readAccessGeoRedundant)
                {
                    options.GeoRedundantSecondaryUri = _grsServiceUri.Value;
                }

                if (_tokenCredential != null)
                {
                    _blobClient = new BlobServiceClient(_primaryServiceUri.Value, _tokenCredential, options);
                }
                else
                {
                    _blobClient = new BlobServiceClient(_storageConnectionString, options);
                }
            }

            return new CloudBlobContainerWrapper(_blobClient.GetBlobContainerClient(containerAddress), this);
        }

        internal BlockBlobClient CreateBlockBlobClient(CloudBlobWrapper original, BlobClientOptions newOptions)
        {
            if (_readAccessGeoRedundant)
            {
                newOptions.GeoRedundantSecondaryUri = _grsServiceUri.Value;
            }
            if (_tokenCredential != null)
            {
                return new BlockBlobClient(original.Uri, _tokenCredential, newOptions);
            }
            return new BlockBlobClient(_storageConnectionString, original.Container, original.Name, newOptions);
        }

        internal BlobServiceClient Client => _blobClient;
        internal bool UsingTokenCredential => _tokenCredential != null;

        private Uri GetPrimaryUri()
        {
            var tempClient = new BlobServiceClient(_storageConnectionString);
            return tempClient.Uri;
        }

        private Uri GetGrsUri()
        {
            var uriBuilder = new UriBuilder(_primaryServiceUri.Value);
            var hostParts = uriBuilder.Host.Split('.');
            hostParts[0] = hostParts[0] + "-secondary";
            uriBuilder.Host = string.Join(".", hostParts);
            return uriBuilder.Uri;
        }
    }
}
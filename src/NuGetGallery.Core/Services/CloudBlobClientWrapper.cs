﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private const string SecondaryHostPostfix = "-secondary";
        private readonly string _storageConnectionString;
        private readonly bool _readAccessGeoRedundant = false;
        private readonly Lazy<Uri> _primaryServiceUri;
        private readonly Lazy<Uri> _grsServiceUri;
        private readonly Lazy<BlobServiceClient> _blobClient;
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
            _blobClient = new Lazy<BlobServiceClient>(CreateBlobServiceClient);
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
                    new AzureSasCredential(uri.Query)), null);
            }
            else
            {
                blob = new CloudBlobWrapper(new BlockBlobClient(uri), null);
            }

            return blob;
        }

        public ICloudBlobContainer GetContainerReference(string containerAddress)
        {
            return new CloudBlobContainerWrapper(_blobClient.Value.GetBlobContainerClient(containerAddress), this);
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

        internal BlobContainerClient CreateBlobContainerClient(CloudBlobLocationMode locationMode, string containerName)
        {
            if ((locationMode == CloudBlobLocationMode.PrimaryThenSecondary)
                || (!_readAccessGeoRedundant && locationMode == CloudBlobLocationMode.PrimaryOnly))
            {
                // Requested location mode is the same as we expect.
                // If we are not supposed to be using RA-GRS, then there is no difference between PrimaryOnly and PrimaryThenSecondary
                return null;
            }

            if (locationMode == CloudBlobLocationMode.SecondaryOnly)
            {
                if (!_readAccessGeoRedundant)
                {
                    throw new InvalidOperationException("Can't get secondary region for non RA-GRS storage services");
                }
                var service = CreateSecondaryBlobServiceClient(options: null);
                return service.GetBlobContainerClient(containerName);
            }
            if (locationMode == CloudBlobLocationMode.PrimaryOnly)
            {
                var service = CreateBlobServiceClient(readAccessGeoRedundant: false);
                return service.GetBlobContainerClient(containerName);
            }
            throw new ArgumentOutOfRangeException(nameof(locationMode));
        }

        internal BlobServiceClient Client => _blobClient.Value;
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
            hostParts[0] = hostParts[0] + SecondaryHostPostfix;
            uriBuilder.Host = string.Join(".", hostParts);
            return uriBuilder.Uri;
        }

        private BlobServiceClient CreateBlobServiceClient()
        {
            return CreateBlobServiceClient(_readAccessGeoRedundant);
        }

        private BlobServiceClient CreateBlobServiceClient(bool readAccessGeoRedundant)
        {
            var options = new BlobClientOptions();
            if (readAccessGeoRedundant)
            {
                options.GeoRedundantSecondaryUri = _grsServiceUri.Value;
            }

            return CreateBlobServiceClient(options);
        }

        private BlobServiceClient CreateBlobServiceClient(BlobClientOptions options)
        {
            if (_tokenCredential != null)
            {
                return new BlobServiceClient(_primaryServiceUri.Value, _tokenCredential, options);
            }
            return new BlobServiceClient(_storageConnectionString, options);
        }

        private BlobServiceClient CreateSecondaryBlobServiceClient(BlobClientOptions options)
        {
            if (_tokenCredential != null)
            {
                return new BlobServiceClient(_grsServiceUri.Value, _tokenCredential, options);
            }
            string secondaryConnectionString = GetSecondaryConnectionString();
            return new BlobServiceClient(secondaryConnectionString, options);
        }

        private string GetSecondaryConnectionString()
        {
            var primaryAccountName = _primaryServiceUri.Value.Host.Split('.')[0];
            var secondaryAccountName = _grsServiceUri.Value.Host.Split('.')[0];
            var secondaryConnectionString = _storageConnectionString
                .Replace($"https://{primaryAccountName}.", $"https://{secondaryAccountName}.")
                .Replace($"AccountName={primaryAccountName};", $"AccountName={secondaryAccountName};");
            return secondaryConnectionString;
        }
    }
}
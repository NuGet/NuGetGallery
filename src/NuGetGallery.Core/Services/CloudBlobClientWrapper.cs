// Copyright (c) .NET Foundation. All rights reserved.
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
        private readonly TimeSpan? _requestTimeout = null;
        private readonly Lazy<Uri> _primaryServiceUri;
        private readonly Lazy<Uri> _secondaryServiceUri;
        private readonly Lazy<BlobServiceClient> _blobClient;
        private readonly TokenCredential _tokenCredential = null;

        public CloudBlobClientWrapper(string storageConnectionString, bool readAccessGeoRedundant = false, TimeSpan? requestTimeout = null)
            : this(storageConnectionString)
        {
            _readAccessGeoRedundant = readAccessGeoRedundant;
            _requestTimeout = requestTimeout; // OK to be null
        }

        private CloudBlobClientWrapper(string storageConnectionString, TokenCredential tokenCredential)
            : this(storageConnectionString)
        {
            _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
        }

        private CloudBlobClientWrapper(string storageConnectionString)
        {
            // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
            _storageConnectionString = storageConnectionString.Replace("SharedAccessSignature=?", "SharedAccessSignature=");
            _primaryServiceUri = new Lazy<Uri>(GetPrimaryServiceUri);
            _secondaryServiceUri = new Lazy<Uri>(GetSecondaryServiceUri);
            _blobClient = new Lazy<BlobServiceClient>(CreateBlobServiceClient);
        }

        public static CloudBlobClientWrapper UsingMsi(string storageConnectionString, string clientId = null)
        {
            var tokenCredential = new ManagedIdentityCredential(clientId);
            return new CloudBlobClientWrapper(storageConnectionString, tokenCredential);
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
                    new AzureSasCredential(uri.Query)), uri);
            }
            else
            {
                blob = new CloudBlobWrapper(new BlockBlobClient(uri), container: null);
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
                newOptions.GeoRedundantSecondaryUri = _secondaryServiceUri.Value;
            }
            if (_tokenCredential != null)
            {
                return new BlockBlobClient(original.Uri, _tokenCredential, newOptions);
            }
            return new BlockBlobClient(_storageConnectionString, original.Container, original.Name, newOptions);
        }

        internal BlobContainerClient CreateBlobContainerClient(string containerName, TimeSpan requestTimeout)
        {
            if (containerName == null)
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            var newService = CreateBlobServiceClient(CreateBlobOptions(_readAccessGeoRedundant, requestTimeout));
            return newService.GetBlobContainerClient(containerName);
        }

        internal BlobContainerClient CreateBlobContainerClient(CloudBlobLocationMode locationMode, string containerName, TimeSpan? requestTimeout = null)
        {
            if (containerName == null)
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            if ((locationMode == CloudBlobLocationMode.PrimaryThenSecondary)
                || (!_readAccessGeoRedundant && locationMode == CloudBlobLocationMode.PrimaryOnly))
            {
                // Requested location mode is the same as we expect.
                // If we are not supposed to be using RA-GRS, then there is no difference between PrimaryOnly and PrimaryThenSecondary
                if (requestTimeout.HasValue)
                {
                    return CreateBlobContainerClient(containerName, requestTimeout.Value);
                }
                return null;
            }

            if (locationMode == CloudBlobLocationMode.SecondaryOnly)
            {
                if (!_readAccessGeoRedundant)
                {
                    throw new InvalidOperationException("Can't get secondary region for non RA-GRS storage services");
                }
                var service = CreateSecondaryBlobServiceClient(CreateBlobOptions(readAccessGeoRedundant: false, requestTimeout));
                return service.GetBlobContainerClient(containerName);
            }
            if (locationMode == CloudBlobLocationMode.PrimaryOnly)
            {
                var service = CreateBlobServiceClient(CreateBlobOptions(readAccessGeoRedundant: false, requestTimeout));
                return service.GetBlobContainerClient(containerName);
            }
            throw new ArgumentOutOfRangeException(nameof(locationMode));
        }

        internal BlobServiceClient Client => _blobClient.Value;
        internal bool UsingTokenCredential => _tokenCredential != null;

        private Uri GetPrimaryServiceUri()
        {
            var tempClient = new BlobServiceClient(_storageConnectionString);
            // if _storageConnectionString has SAS token, Uri will contain SAS signature, we need to strip it
            var uriBuilder = new UriBuilder(tempClient.Uri);
            uriBuilder.Query = "";
            uriBuilder.Fragment = "";
            return uriBuilder.Uri;
        }

        private Uri GetSecondaryServiceUri()
        {
            var uriBuilder = new UriBuilder(_primaryServiceUri.Value);
            var hostParts = uriBuilder.Host.Split('.');
            hostParts[0] = hostParts[0] + SecondaryHostPostfix;
            uriBuilder.Host = string.Join(".", hostParts);
            return uriBuilder.Uri;
        }

        private BlobServiceClient CreateBlobServiceClient()
        {
            return CreateBlobServiceClient(CreateBlobOptions(_readAccessGeoRedundant));
        }

        private BlobClientOptions CreateBlobOptions(bool readAccessGeoRedundant, TimeSpan? requestTimeout = null)
        {
            var options = new BlobClientOptions();
            if (readAccessGeoRedundant)
            {
                options.GeoRedundantSecondaryUri = _secondaryServiceUri.Value;
            }
            if (requestTimeout.HasValue)
            {
                options.Retry.NetworkTimeout = requestTimeout.Value;
            }
            else if (_requestTimeout.HasValue)
            {
                options.Retry.NetworkTimeout = _requestTimeout.Value;
            }

            return options;
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
                return new BlobServiceClient(_secondaryServiceUri.Value, _tokenCredential, options);
            }
            string secondaryConnectionString = GetSecondaryConnectionString();
            return new BlobServiceClient(secondaryConnectionString, options);
        }

        private string GetSecondaryConnectionString()
        {
            var primaryAccountName = _primaryServiceUri.Value.Host.Split('.')[0];
            var secondaryAccountName = _secondaryServiceUri.Value.Host.Split('.')[0];
            var secondaryConnectionString = _storageConnectionString
                .Replace($"https://{primaryAccountName}.", $"https://{secondaryAccountName}.")
                .Replace($"AccountName={primaryAccountName};", $"AccountName={secondaryAccountName};");
            return secondaryConnectionString;
        }
    }
}
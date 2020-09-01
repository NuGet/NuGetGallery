// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private CloudBlobClient _cloudBlobClient;

        public CloudBlobClientWrapper(string storageConnectionString, bool readAccessGeoRedundant)
        {
            _cloudBlobClient = CloudBlobClientFactory.CreateCloudBlobClient(storageConnectionString);

            if (readAccessGeoRedundant)
            {
                _cloudBlobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
            }
        }

        public CloudBlobClientWrapper(string storageConnectionString, BlobRequestOptions defaultRequestOptions)
        {
            _cloudBlobClient = CloudBlobClientFactory.CreateCloudBlobClient(storageConnectionString);

            if (defaultRequestOptions != null)
            {
                _cloudBlobClient.DefaultRequestOptions = defaultRequestOptions;
            }
        }

        public CloudBlobClientWrapper(string sasToken, string storageAccountName, bool readAccessGeoRedundant)
        {
            _cloudBlobClient = CloudBlobClientFactory.CreateCloudBlobClient(sasToken, storageAccountName);

            if (readAccessGeoRedundant)
            {
                _cloudBlobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
            }
        }

        public CloudBlobClientWrapper(string sasToken, string storageAccountName, BlobRequestOptions defaultRequestOptions)
        {
            _cloudBlobClient = CloudBlobClientFactory.CreateCloudBlobClient(sasToken, storageAccountName);

            if (defaultRequestOptions != null)
            {
                _cloudBlobClient.DefaultRequestOptions = defaultRequestOptions;
            }
        }

        public ISimpleCloudBlob GetBlobFromUri(Uri uri)
        {
            // For Azure blobs, the query string is assumed to be the SAS token.
            ISimpleCloudBlob blob;
            if (!string.IsNullOrEmpty(uri.Query))
            {
                var uriBuilder = new UriBuilder(uri);
                uriBuilder.Query = string.Empty;

                blob = new CloudBlobWrapper(new CloudBlockBlob(
                    uriBuilder.Uri,
                    new StorageCredentials(uri.Query)));
            }
            else
            {
                blob = new CloudBlobWrapper(new CloudBlockBlob(uri));
            }

            return blob;
        }

        public ICloudBlobContainer GetContainerReference(string containerAddress)
        {
            return new CloudBlobContainerWrapper(_cloudBlobClient.GetContainerReference(containerAddress));
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private readonly string _storageConnectionString;
        private readonly BlobRequestOptions _defaultRequestOptions;
        private readonly bool _readAccessGeoRedundant;
        private CloudBlobClient _blobClient;

        private DateTime _createdAt;

        public CloudBlobClientWrapper(string storageConnectionString, bool readAccessGeoRedundant)
        {
            _storageConnectionString = storageConnectionString;
            _readAccessGeoRedundant = readAccessGeoRedundant;
            _createdAt = DateTime.UtcNow;
        }

        public CloudBlobClientWrapper(string storageConnectionString, BlobRequestOptions defaultRequestOptions)
        {
            _storageConnectionString = storageConnectionString;
            _defaultRequestOptions = defaultRequestOptions;
            _createdAt = DateTime.UtcNow;
        }

        public ISimpleCloudBlob GetBlobFromUri(Uri uri)
         {
            // TODO: Remove
            Trace.TraceInformation($"CloudBlobClientWrapper age: {DateTime.UtcNow - _createdAt}");

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
            // TODO: Remove
            Trace.TraceInformation($"CloudBlobClientWrapper age: {DateTime.UtcNow - _createdAt}");

            if (_blobClient == null)
            {
                _blobClient = CloudStorageAccount.Parse(_storageConnectionString).CreateCloudBlobClient();

                if (_readAccessGeoRedundant)
                {
                    _blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
                }
                else if (_defaultRequestOptions != null)
                {
                    _blobClient.DefaultRequestOptions = _defaultRequestOptions;
                }
            }

            return new CloudBlobContainerWrapper(_blobClient.GetContainerReference(containerAddress));
        }
    }
}
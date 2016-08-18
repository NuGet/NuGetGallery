// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private readonly string _storageConnectionString;
        private readonly bool _readAccessGeoRedundant;
        private CloudBlobClient _blobClient;

        public CloudBlobClientWrapper(string storageConnectionString, bool readAccessGeoRedundant)
        {
            _storageConnectionString = storageConnectionString;
            _readAccessGeoRedundant = readAccessGeoRedundant;
        }

        public ICloudBlobContainer GetContainerReference(string containerAddress)
        {
            if (_blobClient == null)
            {
                _blobClient = CloudStorageAccount.Parse(_storageConnectionString).CreateCloudBlobClient();

                if (_readAccessGeoRedundant)
                {
                    _blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
                }
            }
            return new CloudBlobContainerWrapper(_blobClient.GetContainerReference(containerAddress));
        }
    }
}
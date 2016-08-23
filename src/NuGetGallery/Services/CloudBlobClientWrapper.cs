// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGetGallery.Configuration;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private IGalleryConfigurationService _configService;
        private string _storageConnectionString;
        private bool _readAccessGeoRedundant;
        private CloudBlobClient _blobClient;

        public CloudBlobClientWrapper(IGalleryConfigurationService configService)
        {
            _configService = configService;
        }

        public async Task<ICloudBlobContainer> GetContainerReference(string containerAddress)
        {
            var oldStorageConnectionString = _storageConnectionString;
            var oldReadAccessGeoRedundant = _readAccessGeoRedundant;

            _storageConnectionString = (await _configService.GetCurrent()).AzureStorageConnectionString;
            _readAccessGeoRedundant = (await _configService.GetCurrent()).AzureStorageReadAccessGeoRedundant;

            if (_blobClient == null || oldStorageConnectionString != _storageConnectionString || oldReadAccessGeoRedundant != _readAccessGeoRedundant)
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
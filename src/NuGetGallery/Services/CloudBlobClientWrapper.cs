// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGetGallery.Configuration;
using System.Threading.Tasks;
using NuGetGallery.Configuration.Factory;

namespace NuGetGallery
{
    public class CloudBlobClientWrapper : ICloudBlobClient
    {
        private readonly ConfigObjectDelegate<CloudBlobClient> _blobClientDelgate;
        private readonly IGalleryConfigurationService _configService;

        public CloudBlobClientWrapper(IGalleryConfigurationService configService)
        {
            _configService = configService;

            _blobClientDelgate = new ConfigObjectDelegate<CloudBlobClient>(parameters =>
                {
                    var blobClient = CloudStorageAccount.Parse((string) parameters[0]).CreateCloudBlobClient();

                    if ((bool) parameters[1])
                    {
                        blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
                    }

                    return blobClient;
                },
                new[]
                {
                    nameof(IAppConfiguration.AzureStorageConnectionString),
                    nameof(IAppConfiguration.AzureStorageReadAccessGeoRedundant)
                });
        }

        public async Task<ICloudBlobContainer> GetContainerReference(string containerAddress)
        {
            return new CloudBlobContainerWrapper((await _blobClientDelgate.GetAsync(_configService)).GetContainerReference(containerAddress));
        }
    }
}
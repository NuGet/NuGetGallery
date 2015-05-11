// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public class CloudBlobContainerWrapper : ICloudBlobContainer
    {
        private readonly CloudBlobContainer _blobContainer;

        public CloudBlobContainerWrapper(CloudBlobContainer blobContainer)
        {
            _blobContainer = blobContainer;
        }

        public Task CreateIfNotExistAsync()
        {
            return Task.Factory.FromAsync<bool>(
                _blobContainer.BeginCreateIfNotExists(null, null), 
                _blobContainer.EndCreateIfNotExists);
        }

        public Task SetPermissionsAsync(BlobContainerPermissions permissions)
        {
            return Task.Factory.FromAsync(
                _blobContainer.BeginSetPermissions(permissions, null, null),
                _blobContainer.EndSetPermissions);
        }

        public ISimpleCloudBlob GetBlobReference(string blobAddressUri)
        {
            return new CloudBlobWrapper(_blobContainer.GetBlockBlobReference(blobAddressUri));
        }
    }
}

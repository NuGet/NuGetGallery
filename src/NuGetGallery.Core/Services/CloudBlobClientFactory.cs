// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Services
{
    public class CloudBlobClientFactory
    {
        public static CloudBlobClient CreateCloudBlobClient(string sasToken, string storageAccountName)
        {
            var cloudStorageAccount = CloudStorageAccountFactory.CreateCloudStorageAccount(sasToken, storageAccountName);

            return cloudStorageAccount.CreateCloudBlobClient();
        }

        public static CloudBlobClient CreateCloudBlobClient(string storageConnectionString)
        {
            var cloudStorageAccount = CloudStorageAccountFactory.CreateCloudStorageAccount(storageConnectionString);

            return cloudStorageAccount.CreateCloudBlobClient();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace NuGetGallery.Services
{
    public class CloudStorageAccountFactory
    {
        public static CloudStorageAccount CreateCloudStorageAccount(string storageConnectionString)
        {
            return CloudStorageAccount.Parse(storageConnectionString);
        }
        
        public static CloudStorageAccount CreateCloudStorageAccount(string sasToken, string storageAccountName)
        {
            var storageCredentials = new StorageCredentials(sasToken);

            return new CloudStorageAccount(storageCredentials, storageAccountName, endpointSuffix: null, useHttps: true);
        }
    }
}
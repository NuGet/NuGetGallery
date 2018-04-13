// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.V3PerPackage
{
    public static class BlobStorageUtilities
    {
        public static CloudBlobClient GetBlobClient(GlobalContext context)
        {
            var storageCredentials = new StorageCredentials(context.StorageAccountName, context.StorageKeyValue);
            var storageAccount = new CloudStorageAccount(storageCredentials, useHttps: true);
            return storageAccount.CreateCloudBlobClient();
        }
    }
}

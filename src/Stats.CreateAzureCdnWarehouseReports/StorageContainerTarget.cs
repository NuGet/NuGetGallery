// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class StorageContainerTarget
    {
        public StorageContainerTarget(BlobServiceClient storageAccount, string containerName)
        {
            StorageAccount = storageAccount;
            ContainerName = containerName;
        }

        public BlobServiceClient StorageAccount { get; set; }
        public string ContainerName { get; set; }
    }
}

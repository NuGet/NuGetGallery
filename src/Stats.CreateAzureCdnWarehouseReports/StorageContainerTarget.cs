// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class StorageContainerTarget
    {
        public StorageContainerTarget(CloudStorageAccount storageAccount, string containerName)
        {
            StorageAccount = storageAccount;
            ContainerName = containerName;
        }

        public CloudStorageAccount StorageAccount { get; set; }
        public string ContainerName { get; set; }
    }
}
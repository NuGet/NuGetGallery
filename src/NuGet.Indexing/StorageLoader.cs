// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System.IO;

namespace NuGet.Indexing
{
    public class StorageLoader : ILoader
    {
        CloudStorageAccount _storageAccount;
        string _containerName;

        public StorageLoader(CloudStorageAccount storageAccount, string containerName)
        {
            _storageAccount = storageAccount;
            _containerName = containerName;
        }

        public JsonReader GetReader(string name)
        {
            CloudBlobClient client = _storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(_containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            return new JsonTextReader(new StreamReader(blob.OpenRead()));
        }
    }
}

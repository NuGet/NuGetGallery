// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace NuGet.Indexing
{
    public class StorageDownloadLookup : DownloadLookup
    {
        CloudBlockBlob _blob;

        public override string Path { get { return _blob.Uri.AbsoluteUri; } }

        public StorageDownloadLookup(CloudStorageAccount storageAccount, string containerName, string blobName)
        {
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(containerName);
            _blob = container.GetBlockBlobReference(blobName);
        }

        protected override JsonReader GetReader()
        {
            if (!_blob.Exists())
            {
                return null;
            }

            return new JsonTextReader(new StreamReader(_blob.OpenRead()));
        }
    }
}
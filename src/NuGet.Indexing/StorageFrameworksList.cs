// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public class StorageFrameworksList : FrameworksList
    {
        private readonly CloudBlockBlob _blob;

        public override string Path { get { return _blob.Uri.AbsoluteUri; } }

        public StorageFrameworksList(string connectionString)
            : this(CloudStorageAccount.Parse(connectionString))
        {
        }

        public StorageFrameworksList(CloudStorageAccount storageAccount)
            : this(storageAccount, "ng-search")
        {
        }

        public StorageFrameworksList(CloudStorageAccount storageAccount, string containerName)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName))
        {
        }

        public StorageFrameworksList(CloudBlobContainer container)
            : this(container.GetBlockBlobReference(@"data/" + FileName))
        {
        }

        public StorageFrameworksList(CloudBlockBlob blob)
        {
            _blob = blob;
        }

        public StorageFrameworksList(CloudStorageAccount storageAccount, string containerName, string path)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName).GetBlockBlobReference(path))
        {
        }

        protected override JObject LoadJson()
        {
            if (!_blob.Exists())
            {
                return null;
            }
            string json = _blob.DownloadText();
            JObject obj = JObject.Parse(json);
            return obj;
        }
    }
}
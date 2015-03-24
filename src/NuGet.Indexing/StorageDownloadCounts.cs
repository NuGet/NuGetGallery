using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public class StorageDownloadCounts : DownloadCounts
    {
        CloudBlockBlob _blob;

        public override string Path { get { return _blob.Uri.AbsoluteUri; } }

        public StorageDownloadCounts(string connectionString) : this(CloudStorageAccount.Parse(connectionString))
        {
        }

        public StorageDownloadCounts(CloudStorageAccount storageAccount) : this(storageAccount, "ng-search")
        {
        }

        public StorageDownloadCounts(CloudStorageAccount storageAccount, string containerName)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName))
        {
        }

        public StorageDownloadCounts(CloudStorageAccount storageAccount, string containerName, string blobName)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName).GetBlockBlobReference(blobName))
        {
        }

        public StorageDownloadCounts(CloudBlobContainer container) : this(container.GetBlockBlobReference(@"data/downloads.v1.json"))
        {
        }

        public StorageDownloadCounts(CloudBlockBlob blob)
        {
            _blob = blob;
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

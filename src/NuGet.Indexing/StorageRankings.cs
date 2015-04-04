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
    public class StorageRankings : Rankings
    {
        CloudBlockBlob _blob;
        
        public override string Path { get { return _blob.Uri.AbsoluteUri; } }

        public StorageRankings(string connectionString) : this(CloudStorageAccount.Parse(connectionString))
        {
        }

        public StorageRankings(CloudStorageAccount storageAccount) : this(storageAccount, "ng-search")
        {
        }

        public StorageRankings(CloudStorageAccount storageAccount, string containerName)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName))
        {
        }

        public StorageRankings(CloudStorageAccount storageAccount, string containerName, string blobName)
            : this(storageAccount.CreateCloudBlobClient().GetContainerReference(containerName).GetBlockBlobReference(blobName))
        {
        }

        public StorageRankings(CloudBlobContainer container) : this(container.GetBlockBlobReference(@"data/rankings.v1.json"))
        {
        }

        public StorageRankings(CloudBlockBlob blob)
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

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class StorageRankings : Rankings
    {
        string _connectionString;

        public StorageRankings(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override JObject LoadJson()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("ranking");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference("all.json");
            string json = blockBlob.DownloadText();
            JObject obj = JObject.Parse(json);
            return obj;
        }
    }
}

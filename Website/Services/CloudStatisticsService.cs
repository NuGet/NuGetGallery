using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class CloudStatisticsService : IStatisticsService
    {
        private string _connectionString;

        public CloudStatisticsService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public JArray LoadReport(string name)
        {
            string connectionString = _connectionString;

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("popularity");
            CloudBlockBlob blob = container.GetBlockBlobReference(name);

            //TODO: async OpenRead

            string content;
            using (TextReader reader = new StreamReader(blob.OpenRead()))
            {
                content = reader.ReadToEnd();
            }

            return JArray.Parse(content);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace NuGet.Services.Storage
{
    public class BlobStorageHub
    {
        public CloudBlobClient Client { get; private set; }

        public BlobStorageHub(CloudBlobClient client)
        {
            Client = client;
        }

        public virtual Task<CloudBlockBlob> UploadBlob(string contentType, string sourceFileName, string containerName, string path)
        {
            CloudBlobContainer container = Client.GetContainerReference(containerName);
            return container.SafeExecute(async ct =>
            {
                var blob = ct.GetBlockBlobReference(path);
                await blob.UploadFromFileAsync(sourceFileName, FileMode.Open);
                blob.Properties.ContentType = contentType;
                await blob.SetPropertiesAsync();
                return blob;
            });
        }

        public virtual Task<CloudBlockBlob> UploadJsonBlob(object obj, string containerName, string path)
        {
            CloudBlobContainer container = Client.GetContainerReference(containerName);
            return container.SafeExecute(async ct =>
            {
                var blob = ct.GetBlockBlobReference(path);
                await blob.UploadTextAsync(JsonConvert.SerializeObject(obj));
                blob.Properties.ContentType = "application/json";
                await blob.SetPropertiesAsync();
                return blob;
            });
        }

        public virtual Task<CloudBlockBlob> DownloadBlob(string containerName, string path, string destinationFileName)
        {
            CloudBlobContainer container = Client.GetContainerReference(containerName);
            return container.SafeExecute(async ct =>
            {
                var blob = ct.GetBlockBlobReference(path);
                await blob.DownloadToFileAsync(destinationFileName, FileMode.CreateNew);
                return blob;
            });
        }

        public virtual CloudBlockBlob GetBlob(string container, string path)
        {
            return GetBlob(container, path);
        }
    }
}

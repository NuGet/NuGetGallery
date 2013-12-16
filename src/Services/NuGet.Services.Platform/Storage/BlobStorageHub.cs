using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Storage
{
    public class BlobStorageHub
    {
        private const string ContainerNamePrefix = "ng-";

        public CloudBlobClient Client { get; private set; }

        public BlobStorageHub(CloudBlobClient client)
        {
            Client = client;
        }

        public virtual Task<CloudBlockBlob> UploadBlob(string sourceFileName, string containerName, string path)
        {
            CloudBlobContainer container = Client.GetContainerReference(GetFullContainerName(containerName));
            return container.SafeExecute(async ct =>
            {
                var blob = ct.GetBlockBlobReference(path);
                await blob.UploadFromFileAsync(sourceFileName, FileMode.Open);
                return blob;
            });
        }

        public virtual Task<CloudBlockBlob> DownloadBlob(string containerName, string path, string destinationFileName)
        {
            CloudBlobContainer container = Client.GetContainerReference(GetFullContainerName(containerName));
            return container.SafeExecute(async ct =>
            {
                var blob = ct.GetBlockBlobReference(path);
                await blob.DownloadToFileAsync(destinationFileName, FileMode.CreateNew);
                return blob;
            });
        }

        public virtual string GetFullContainerName(string name)
        {
            return ContainerNamePrefix + name;
        }
    }
}

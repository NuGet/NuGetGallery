using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Threading.Tasks;

namespace Catalog.Storage
{
    public class AzureStorage : IStorage
    {
        public AzureStorage()
        {
        }

        public string ConnectionString
        {
            get;
            set;
        }

        public string Container
        {
            get;
            set;
        }

        public string BaseAddress
        {
            get;
            set;
        }

        public bool Verbose
        {
            get;
            set;
        }

        //  save
       
        public async Task Save(string contentType, string name, string content)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(ConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Container);

            if (container.CreateIfNotExists())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                Console.WriteLine("Created '{0}' publish container", Container);
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            blob.Properties.ContentType = contentType;
            blob.Properties.CacheControl = "no-store";  // no for production, just helps with debugging

            await blob.UploadTextAsync(content);

            Console.WriteLine("save: {0}", name);
        }

        //  load

        public async Task<string> Load(string name)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(ConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Container);

            if (container.CreateIfNotExists())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                Console.WriteLine("Created '{0}' publish container", Container);
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);

            if (blob.Exists())
            {
                string content = await blob.DownloadTextAsync();
                return content;
            }

            return null;
        }
    }
}

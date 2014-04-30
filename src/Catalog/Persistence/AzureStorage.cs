using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Threading.Tasks;

namespace Catalog.Persistence
{
    public class AzureStorage : Storage
    {
        public AzureStorage()
        {
        }

        public string ConnectionString
        {
            get;
            set;
        }

        //  save
       
        public override async Task Save(string contentType, string name, string content)
        {
            SaveCount++;

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

        public override async Task<string> Load(string name)
        {
            LoadCount++;

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

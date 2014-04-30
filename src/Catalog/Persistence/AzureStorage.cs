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
       
        public override async Task Save(string contentType, Uri resourceUri, string content)
        {
            SaveCount++;

            string name = GetName(resourceUri, BaseAddress, Container);

            if (Verbose)
            {
                Console.WriteLine("save {0}", name);
            }

            CloudStorageAccount account = CloudStorageAccount.Parse(ConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Container);

            if (container.CreateIfNotExists())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                if (Verbose)
                {
                    Console.WriteLine("Created '{0}' publish container", Container);
                }
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            blob.Properties.ContentType = contentType;
            blob.Properties.CacheControl = "no-store";  // no for production, just helps with debugging

            await blob.UploadTextAsync(content);
        }

        //  load

        public override async Task<string> Load(Uri resourceUri)
        {
            LoadCount++;

            string name = GetName(resourceUri, BaseAddress, Container);

            CloudStorageAccount account = CloudStorageAccount.Parse(ConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Container);

            if (container.CreateIfNotExists())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                if (Verbose)
                {
                    Console.WriteLine("Created '{0}' publish container", Container);
                }
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

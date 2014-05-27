using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorage : Storage
    {
        public AzureStorage()
        {
        }

        // "DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}"

        public string ConnectionString
        {
            get;
            set;
        }

        // if the ConnectionString is null the follow are used 

        public string AccountName
        {
            get;
            set;
        }

        public string AccountKey
        {
            get;
            set;
        }

        //  save
       
        public override async Task Save(Uri resourceUri, StorageContent content)
        {
            SaveCount++;

            string name = GetName(resourceUri, BaseAddress, Container);

            if (Verbose)
            {
                Console.WriteLine("save {0}", name);
            }

            CloudStorageAccount account = ConnectionString != null ?
                CloudStorageAccount.Parse(ConnectionString) : new CloudStorageAccount(new StorageCredentials(AccountName, AccountKey), true);

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
            blob.Properties.ContentType = content.ContentType;
            blob.Properties.CacheControl = "no-store";  // no for production, just helps with debugging

            using (Stream stream = content.GetContentStream())
            {
                await blob.UploadFromStreamAsync(stream);
            }
        }

        //  load

        public override async Task<StorageContent> Load(Uri resourceUri)
        {
            LoadCount++;

            string name = GetName(resourceUri, BaseAddress, Container);

            CloudStorageAccount account = ConnectionString != null ?
                CloudStorageAccount.Parse(ConnectionString) : new CloudStorageAccount(new StorageCredentials(AccountName, AccountKey), true);

            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Container);

            CloudBlockBlob blob = container.GetBlockBlobReference(name);

            if (blob.Exists())
            {
                string content = await blob.DownloadTextAsync();
                return new StringStorageContent(content);
            }

            return null;
        }

        //  delete

        public override async Task Delete(Uri resourceUri)
        {
            DeleteCount++;

            string name = GetName(resourceUri, BaseAddress, Container);

            CloudStorageAccount account = ConnectionString != null ?
                CloudStorageAccount.Parse(ConnectionString) : new CloudStorageAccount(new StorageCredentials(AccountName, AccountKey), true);

            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Container);

            CloudBlockBlob blob = container.GetBlockBlobReference(name);

            await blob.DeleteAsync();
        }
    }
}

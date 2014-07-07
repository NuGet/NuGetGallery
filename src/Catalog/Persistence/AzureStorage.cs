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
        private CloudBlobDirectory _directory;

        public AzureStorage(CloudStorageAccount account, string containerName) : this(account, containerName, String.Empty) { }
        public AzureStorage(CloudStorageAccount account, string containerName, string path)
            : this(account.CreateCloudBlobClient().GetContainerReference(containerName).GetDirectoryReference(path))
        {
        }

        public AzureStorage(CloudBlobDirectory directory)
        {
            _directory = directory;
            BaseAddress = new UriBuilder(_directory.Uri)
            {
                Scheme = "http" // Convert base address to http. 'https' can be used for communication but is not part of the names.
            }.Uri;
        }

        //  save
       
        public override async Task Save(Uri resourceUri, StorageContent content)
        {
            SaveCount++;

            string name = GetName(resourceUri);

            if (Verbose)
            {
                Console.WriteLine("save {0}", name);
            }

            if (_directory.Container.CreateIfNotExists())
            {
                _directory.Container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                if (Verbose)
                {
                    Console.WriteLine("Created '{0}' publish container", _directory.Container.Name);
                }
            }

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);
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

            string name = GetName(resourceUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);

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

            string name = GetName(resourceUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);

            await blob.DeleteAsync();
        }
    }
}

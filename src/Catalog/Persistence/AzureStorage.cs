using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorage : Storage
    {
        private CloudBlobDirectory _directory;

        public AzureStorage(CloudStorageAccount account, string containerName)
            : this(account, containerName, String.Empty) { }

        public AzureStorage(CloudStorageAccount account, string containerName, string path, Uri baseAddress)
            : this(account.CreateCloudBlobClient().GetContainerReference(containerName).GetDirectoryReference(path), baseAddress)
        {
        }

        public AzureStorage(CloudStorageAccount account, string containerName, string path)
            : this(account.CreateCloudBlobClient().GetContainerReference(containerName).GetDirectoryReference(path))
        {
        }

        public AzureStorage(CloudBlobDirectory directory)
            : this(directory, GetDirectoryUri(directory))
        {
        }

        public AzureStorage(CloudBlobDirectory directory, Uri baseAddress)
            : base(baseAddress ?? GetDirectoryUri(directory))
        {
            _directory = directory;

            if (_directory.Container.CreateIfNotExists())
            {
                _directory.Container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                if (Verbose)
                {
                    Trace.WriteLine(String.Format("Created '{0}' publish container", _directory.Container.Name));
                }
            }

            ResetStatistics();
        }

        static Uri GetDirectoryUri(CloudBlobDirectory directory)
        {
            Uri uri = new UriBuilder(directory.Uri)
            {
                Scheme = "http",
                Port = 80
            }.Uri;

            return uri;
        }

        //  save

        protected override async Task OnSave(Uri resourceUri, StorageContent content)
        {
            string name = GetName(resourceUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);
            blob.Properties.ContentType = content.ContentType;
            blob.Properties.CacheControl = content.CacheControl;

            using (Stream stream = content.GetContentStream())
            {
                await blob.UploadFromStreamAsync(stream);
            }
        }

        //  load

        protected override async Task<StorageContent> OnLoad(Uri resourceUri)
        {
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

        protected override async Task OnDelete(Uri resourceUri)
        {
            string name = GetName(resourceUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);

            await blob.DeleteAsync();
        }
    }
}

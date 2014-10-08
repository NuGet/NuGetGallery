using Microsoft.WindowsAzure.Storage;
using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorageFactory : StorageFactory
    {
        CloudStorageAccount _account;
        string _containerName;
        string _path;

        public AzureStorageFactory(CloudStorageAccount account, string containerName, string path = null)
        {
            _account = account;
            _containerName = containerName;
            _path = path;

            Uri blobEndpoint = new UriBuilder(account.BlobEndpoint)
            {
                Scheme = "http", // Convert base address to http. 'https' can be used for communication but is not part of the names.
                Port = 80
            }.Uri;

            BaseAddress = new Uri(blobEndpoint, containerName + "/" + _path ?? string.Empty);
        }
        public override Storage Create(string name)
        {
            string path = (_path == null) ? name : _path + "/" + name;

            return new AzureStorage(_account, _containerName, path);
        }
    }
}

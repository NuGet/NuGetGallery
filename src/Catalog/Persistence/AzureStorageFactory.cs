using Microsoft.WindowsAzure.Storage;
using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorageFactory : StorageFactory
    {
        CloudStorageAccount _account;
        string _containerName;

        public AzureStorageFactory(CloudStorageAccount account, string containerName)
        {
            _account = account;
            _containerName = containerName;

            Uri blobEndpoint = new UriBuilder(account.BlobEndpoint)
            {
                Scheme = "http", // Convert base address to http. 'https' can be used for communication but is not part of the names.
                Port = 80
            }.Uri;

            BaseAddress = new Uri(blobEndpoint, containerName + "/");
        }
        public override Storage Create(string name)
        {
            return new AzureStorage(_account, _containerName, name);
        }
    }
}

using Microsoft.WindowsAzure.Storage;
using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorageFactory : StorageFactory
    {
        CloudStorageAccount _account;
        string _containerName;
        string _path;
        private Uri _differentBaseAddress = null;

        public AzureStorageFactory(CloudStorageAccount account, string containerName, string path = null, Uri baseAddress = null)
        {
            _account = account;
            _containerName = containerName;
            _path = null;

            if (path != null)
            {
                _path = path.Trim('/') + '/';
            }

            _differentBaseAddress = baseAddress;

            if (baseAddress == null)
            {

                Uri blobEndpoint = new UriBuilder(account.BlobEndpoint)
                {
                    Scheme = "http", // Convert base address to http. 'https' can be used for communication but is not part of the names.
                    Port = 80
                }.Uri;

                BaseAddress = new Uri(blobEndpoint, containerName + "/" + _path ?? string.Empty);
            }
            else
            {
                Uri newAddress = baseAddress;

                if (path != null)
                {
                    newAddress = new Uri(baseAddress, path + "/");
                }

                BaseAddress = newAddress;
            }
        }
        public override Storage Create(string name = null)
        {
            string path = (_path == null) ? name : _path + name;

            path = (name == null) ? (_path == null ? String.Empty : _path.Trim('/')) : path;

            Uri newBase = _differentBaseAddress;

            if (newBase != null && !string.IsNullOrEmpty(name))
            {
                newBase = new Uri(_differentBaseAddress, name + "/");
            }

            return new AzureStorage(_account, _containerName, path, newBase) { Verbose = Verbose };
        }
    }
}

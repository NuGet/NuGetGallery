using Microsoft.WindowsAzure.Storage;

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
        }
        public override Storage Create(string name)
        {
            return new AzureStorage(_account, _containerName, name);
        }
    }
}

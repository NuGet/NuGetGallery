using Microsoft.WindowsAzure.Storage;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorageFactory : StorageFactory
    {
        CloudStorageAccount _account;

        public AzureStorageFactory(CloudStorageAccount account)
        {
            _account = account;
        }
        public override Storage Create(string name)
        {
            return new AzureStorage(_account, name);
        }
    }
}

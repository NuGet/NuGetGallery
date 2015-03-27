
namespace NuGet.Services.Publish
{
    public class Configuration
    {
        const string DefaultStorageContainerCatalog = "catalog";
        const string DefaultStorageContainerPackages = "artifacts";
        const string DefaultStorageContainerOwnership = "ownership";

        public static readonly string StoragePrimary;
        public static readonly string StorageContainerCatalog;
        public static readonly string StorageContainerArtifacts;
        public static readonly string StorageContainerOwnership;
        public static readonly string CatalogBaseAddress;

        static Configuration()
        {
            var configurationService = new ConfigurationService();

            StoragePrimary = configurationService.Get("Storage.Primary");
            StorageContainerCatalog = configurationService.Get("Storage.Container.Catalog") ?? DefaultStorageContainerCatalog;
            StorageContainerArtifacts = configurationService.Get("Storage.Container.Artifacts") ?? DefaultStorageContainerPackages;
            StorageContainerOwnership = configurationService.Get("Storage.Container.Ownership") ?? DefaultStorageContainerOwnership;
            CatalogBaseAddress = configurationService.Get("Catalog.BaseAddress");
        }
    }
}
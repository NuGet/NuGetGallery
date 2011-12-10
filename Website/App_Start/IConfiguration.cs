
namespace NuGetGallery
{
    public interface IConfiguration
    {
        string SiteRoot { get; }
        string AzureStorageAccessKey { get; }
        string AzureStorageAccountName { get; }
        string AzureStorageBlobUrl { get; }
        string FileStorageDirectory { get; }
        PackageStoreType PackageStoreType { get; }
    }
}
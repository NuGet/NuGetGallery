
namespace NuGetGallery
{
    public interface IConfiguration
    {
        string AzureCacheAuthenticationToken { get; }
        string AzureCacheServiceUrl { get; }
        string AzureStorageAccessKey { get; }
        string AzureStorageAccountName { get; }
        string AzureStorageBlobUrl { get; }
        string FileStorageDirectory { get; }
        PackageStoreType PackageStoreType { get; }

        string GetSiteRoot(bool useHttps);
    }
}
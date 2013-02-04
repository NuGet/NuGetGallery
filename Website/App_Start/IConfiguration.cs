namespace NuGetGallery
{
    public interface IConfiguration
    {
        string AzureStorageAccessKey { get; }
        string AzureStorageAccountName { get; }
        string AzureStorageBlobUrl { get; }
        string FileStorageDirectory { get; }
        string AzureCdnHost { get; }
        PackageStoreType PackageStoreType { get; }
        bool UseEmulator { get; }

        string FacebookAppID { get; }
        bool IgnoreDuplicateCreate { get; }

        string GetSiteRoot(bool useHttps);
    }
}
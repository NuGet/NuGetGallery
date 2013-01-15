namespace NuGetGallery
{
    public interface IConfiguration
    {
        string AzureDiagnosticsConnectionString { get; }
        string AzureStorageConnectionString { get; }
        string AzureStatisticsConnectionString { get; }
        string FileStorageDirectory { get; }
        string AzureCdnHost { get; }
        PackageStoreType PackageStoreType { get; }
        bool UseEmulator { get; }

        string AzureCacheEndpoint { get; }
        string AzureCacheKey { get; }

        string FacebookAppID { get; }
        string SqlConnectionString { get; }

        string GetSiteRoot(bool useHttps);
    }
}
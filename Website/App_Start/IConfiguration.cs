namespace NuGetGallery
{
    public interface IConfiguration
    {
        bool HasWorker { get; }
        bool RequireSSL { get; }
        int SSLPort { get; }

        string SmtpHost { get; }
        string SmtpUsername { get; }
        string SmtpPassword { get; }
        int? SmtpPort { get ; }
        bool UseSmtp { get ; }
        string GalleryOwnerName { get; }
        string GalleryOwnerEmail { get; }
        bool ConfirmEmailAddresses { get; }
        string EnvironmentName { get; }

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

        bool ReadOnlyMode { get; }
    }
}

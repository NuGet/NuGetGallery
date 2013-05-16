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

        /// <summary>
        /// Gets the URL for the official NuGet packages feed.
        /// </summary>
        /// <value>
        /// The official NuGet packages feed URL.
        /// </value>
        string OfficialNuGetUrl { get; }

        /// <summary>
        /// Gets a value indicating whether this Gallery should be run as a company intranet site.
        /// </summary>
        /// <value>
        ///   <c>true</c> if intranet site; otherwise, <c>false</c>.
        /// </value>
        bool IsIntranetSite { get; }

        /// <summary>
        /// Gets the intranet company URL for the logo at the top of the layout.
        /// </summary>
        /// <value>
        /// The intranet company URL.
        /// </value>
        string IntranetCompanyUrl { get; }
    }
}

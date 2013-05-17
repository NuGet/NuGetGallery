using System;
namespace NuGetGallery.Configuration
{
    public interface IAppConfiguration
    {
        /// <summary>
        /// Gets a boolean inidicating if this environment provides a background worker.
        /// </summary>
        bool HasWorker { get; set; }

        /// <summary>
        /// Gets the name of the environment in which the gallery is deployed
        /// </summary>
        string Environment { get; set; }

        /// <summary>
        /// Gets a setting indicating if SSL is required for all operations once logged in.
        /// </summary>
        bool RequireSSL { get; set; }

        /// <summary>
        /// Gets the port used for SSL
        /// </summary>
        int SSLPort { get; set; }

        /// <summary>
        /// Gets the connection string to use when connecting to azure storage
        /// </summary>
        string AzureStorageConnectionString { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the site requires that email addresses be confirmed
        /// </summary>
        bool ConfirmEmailAddresses { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the site is in read only mode
        /// </summary>
        bool ReadOnlyMode { get; set; }

        /// <summary>
        /// Gets the local directory in which to store files.
        /// </summary>
        string FileStorageDirectory { get; set; }

        /// <summary>
        /// Gets the gallery owner name
        /// </summary>
        string GalleryOwnerName { get; set; }

        /// <summary>
        /// Gets the gallery owner email address
        /// </summary>
        string GalleryOwnerEmail { get; set; }

        /// <summary>
        /// Gets the storage mechanism used by this instance of the gallery
        /// </summary>
        StorageType StorageType { get; set; }

        /// <summary>
        /// Gets the URI of the SMTP host to use. Or null if SMTP is not being used. Use <see cref="NuGetGallery.Configuration.SmtpUri"/> to parse it
        /// </summary>
        Uri SmtpUri { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database
        /// </summary>
        string SqlConnectionString { get; set; }

        /// <summary>
        /// Gets the host name of the Azure CDN being used
        /// </summary>
        string AzureCdnHost { get; set; }

        /// <summary>
        /// Gets the App ID of the Facebook app associated with this deployment
        /// </summary>
        string FacebookAppId { get; set; }

        /// <summary>
        /// Gets the protocol-independent site root
        /// </summary>
        string SiteRoot { get; set; }
    }
}

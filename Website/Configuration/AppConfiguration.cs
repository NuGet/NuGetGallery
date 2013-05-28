using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using System.Web;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery.Configuration
{
    public class AppConfiguration : IAppConfiguration
    {
        public bool HasWorker { get; set; }

        [DefaultValue("Development")]
        public string Environment { get; set; }

        /// <summary>
        /// Gets a setting indicating if SSL is required for all operations once logged in.
        /// </summary>
        [DefaultValue(false)]
        public bool RequireSSL { get; set; }

        /// <summary>
        /// Gets the port used for SSL
        /// </summary>
        [DefaultValue(443)]
        public int SSLPort { get; set; }

        /// <summary>
        /// Gets the connection string to use when connecting to azure storage
        /// </summary>
        public string AzureStorageConnectionString { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the site requires that email addresses be confirmed
        /// </summary>
        [DefaultValue(true)]
        public bool ConfirmEmailAddresses { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the site is in read only mode
        /// </summary>
        public bool ReadOnlyMode { get; set; }

        /// <summary>
        /// Gets the local directory in which to store files.
        /// </summary>
        [DefaultValue("~/App_Data/Files")]
        public string FileStorageDirectory { get; set; }

        /// <summary>
        /// Gets the gallery owner name and email address
        /// </summary>
        [TypeConverter(typeof(MailAddressConverter))]
        public MailAddress GalleryOwner { get; set; }

        /// <summary>
        /// Gets the storage mechanism used by this instance of the gallery
        /// </summary>
        [DefaultValue(StorageType.NotSpecified)]
        public StorageType StorageType { get; set; }

        /// <summary>
        /// Gets the URI of the SMTP host to use. Or null if SMTP is not being used
        /// </summary>
        [DefaultValue(null)]
        public Uri SmtpUri { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database
        /// </summary>
        [Required]
        [DisplayName("SqlServer")]
        public string SqlConnectionString { get; set; }

        /// <summary>
        /// Gets the host name of the Azure CDN being used
        /// </summary>
        public string AzureCdnHost { get; set; }

        /// <summary>
        /// Gets the App ID of the Facebook app associated with this deployment
        /// </summary>
        public string FacebookAppId { get; set; }

        /// <summary>
        /// Gets the protocol-independent site root
        /// </summary>
        public string SiteRoot { get; set; }
    }
}

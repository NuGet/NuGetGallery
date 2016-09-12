// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace NuGetGallery.Configuration
{
    public class AppConfiguration : IAppConfiguration
    {
        [DefaultValue("Development")]
        public string Environment { get; set; }

        [DefaultValue("")]
        public string WarningBanner { get; set; }

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
        /// Gets a setting if Read Access Geo Redundant is enabled in azure storage
        /// </summary>
        public bool AzureStorageReadAccessGeoRedundant { get; set; }

        /// <summary>
        /// Gets the URI to the search service
        /// </summary>
        public Uri ServiceDiscoveryUri { get; set; }

        /// <summary>
        /// Gets the @type for the Search endpoint
        /// </summary>
        public string SearchServiceResourceType { get; set; }

        /// <summary>
        /// Gets the @type for the Autocomplete endpoint
        /// </summary>
        public string AutocompleteServiceResourceType { get; set; }

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
        /// Gets if only service feeds should be registered
        /// </summary>
        public bool FeedOnlyMode { get; set; }

        /// <summary>
        /// Gets the local directory in which to store files.
        /// </summary>
        [DefaultValue("~/App_Data/Files")]
        public string FileStorageDirectory { get; set; }

        /// <summary>
        /// Gets the location in which the Lucene Index is stored
        /// </summary>
        [DefaultValue(LuceneIndexLocation.AppData)]
        public LuceneIndexLocation LuceneIndexLocation { get; set; }

        /// <summary>
        /// Gets the site brand name i.e. 'NuGet Gallery' by default. Cobranding feature.
        /// </summary>
        public string Brand { get; set; }

        /// <summary>
        /// Gets the gallery owner name and email address
        /// </summary>
        [TypeConverter(typeof(MailAddressConverter))]
        public MailAddress GalleryOwner { get; set; }

        /// <summary>
        /// Gets the gallery e-mail from name and email address
        /// </summary>
        [TypeConverter(typeof(MailAddressConverter))]
        public MailAddress GalleryNoReplyAddress { get; set; }

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
        [DefaultValue("NuGetGallery")]
        public string SqlConnectionString { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database for support requests
        /// </summary>
        [Required]
        [DisplayName("SupportRequestSqlServer")]
        [DefaultValue("NuGetGallery")]
        public string SqlConnectionStringSupportRequest { get; set; }

        /// <summary>
        /// Gets the host name of the Azure CDN being used
        /// </summary>
        public string AzureCdnHost { get; set; }

        /// <summary>
        /// Gets the App ID of the Facebook app associated with this deployment
        /// </summary>
        public string FacebookAppId { get; set; }

        /// <summary>
        /// Gets the Application Insights instrumentation key associated with this deployment.
        /// </summary>
        public string AppInsightsInstrumentationKey { get; set; }

        /// <summary>
        /// Gets the Application Insights sampling percentage associated with this deployment.
        /// </summary>
        public double AppInsightsSamplingPercentage { get; set; }

        /// <summary>
        /// Gets the protocol-independent site root
        /// </summary>
        public string SiteRoot { get; set; }

        /// <summary>
        /// Gets the Google Analytics Property ID being used, if any.
        /// </summary>
        public string GoogleAnalyticsPropertyId { get; set; }

        /// <summary>
        /// Gets a boolean indicating if perf logs should be collected
        /// </summary>
        public bool CollectPerfLogs { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the search index should be updated automatically in the background
        /// </summary>
        [DefaultValue(true)]
        public bool AutoUpdateSearchIndex { get; set; }

        /// <summary>
        /// Gets a string indicating which authentication provider(s) are supported for administrators. 
        /// When specified, the gallery will ensure admin users are logging in using any of the specified authentication providers.
        /// Blank means any authentication provider can be used by administrators.
        /// </summary>
        public string EnforcedAuthProviderForAdmin { get; set; }

        /// <summary>
        /// A regex to validate password format. The default regex requires the password to be atlease 8 characters, 
        /// include at least one uppercase letter, one lowercase letter and a digit.
        /// </summary>
        [Required]
        [DefaultValue("^(?=.*[A-Z])(?=.*[a-z])(?=.*[0-9]).{8,64}$")]
        public string UserPasswordRegex { get; set; }

        [Required]
        [DefaultValue("Your password must be at least 8 characters, should include at least one uppercase letter, one lowercase letter and a digit.")]
        public string UserPasswordHint { get; set; }

        /// <summary>
        /// Defines the time after which V1 API keys expire.
        /// </summary>
        public int ExpirationInDaysForApiKeyV1 { get; set; }

        /// <summary>
        /// Defines the number of days before the API key expires when the server should emit a warning to the client.
        /// </summary>
        public int WarnAboutExpirationInDaysForApiKeyV1 { get; set; }

        /// <summary>
        /// Gets a string containing the PagerDuty account name.
        /// </summary>
        public string PagerDutyAccountName { get; set; }

        /// <summary>
        /// Gets a string containing the PagerDuty API key.
        /// </summary>
        public string PagerDutyAPIKey { get; set; }

        /// <summary>
        /// Gets a string containing the PagerDuty Service key.
        /// </summary>
        public string PagerDutyServiceKey { get; set; }
    }
}

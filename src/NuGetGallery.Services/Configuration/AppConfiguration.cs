// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using NuGet.Services.Configuration;

namespace NuGetGallery.Configuration
{
    public class AppConfiguration : IAppConfiguration
    {
        private string _ExternalBrandingMessage;

        [DefaultValue(ServicesConstants.DevelopmentEnvironment)]
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
        /// A string containing a path exluded from forcing the HTTP to HTTPS redirection. 
        /// To provide multiple paths separate them with ;
        /// </summary>
        [DefaultValue(null)]
        [TypeConverter(typeof(StringArrayConverter))]
        public string[] ForceSslExclusion { get; set; }

        [DisplayName("AzureStorage.Auditing.ConnectionString")]
        public string AzureStorage_Auditing_ConnectionString { get; set; }

        [DisplayName("AzureStorage.UserCertificates.ConnectionString")]
        public string AzureStorage_UserCertificates_ConnectionString { get; set; }

        [DisplayName("AzureStorage.Content.ConnectionString")]
        public string AzureStorage_Content_ConnectionString { get; set; }

        [DisplayName("AzureStorage.Packages.ConnectionString")]
        public string AzureStorage_Packages_ConnectionString { get; set; }

        [DisplayName("AzureStorage.FlatContainer.ConnectionString")]
        public string AzureStorage_FlatContainer_ConnectionString { get; set; }

        [DisplayName("AzureStorage.Statistics.ConnectionString")]
        public string AzureStorage_Statistics_ConnectionString { get; set; }

        [DisplayName("AzureStorage.Statistics.ConnectionString.Alternate")]
        public string AzureStorage_Statistics_ConnectionString_Alternate { get; set; }

        [DisplayName("AzureStorage.Uploads.ConnectionString")]
        public string AzureStorage_Uploads_ConnectionString { get; set; }

        [DisplayName("AzureStorage.Revalidation.ConnectionString")]
        public string AzureStorage_Revalidation_ConnectionString { get; set; }

        /// <summary>
        /// Gets a setting if Read Access Geo Redundant is enabled in azure storage
        /// </summary>
        public bool AzureStorageReadAccessGeoRedundant { get; set; }

        public TimeSpan FeatureFlagsRefreshInterval { get; set; }

        [DefaultValue(true)]
        public bool AdminPanelEnabled { get; set; }

        public bool AdminPanelDatabaseAccessEnabled { get; set; }

        public bool AsynchronousPackageValidationEnabled { get; set; }

        public bool BlockingAsynchronousPackageValidationEnabled { get; set; }

        public bool SelfServiceAccountDeleteEnabled { get; set; }

        public TimeSpan AsynchronousPackageValidationDelay { get; set; }

        public TimeSpan ValidationExpectedTime { get; set; }

        public bool DeprecateNuGetPasswordLogins { get; set; }

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
        [DefaultValue(NuGetGallery.Configuration.StorageType.NotSpecified)]
        public string StorageType { get; set; }

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
        /// Gets the SQL Connection string used to connect to the database read only replica.
        /// </summary>
        [DisplayName("SqlServerReadOnlyReplica")]
        [DefaultValue(null)]
        public string SqlReadOnlyReplicaConnectionString { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database for support requests
        /// </summary>
        [Required]
        [DisplayName("SupportRequestSqlServer")]
        [DefaultValue("NuGetGallery")]
        public string SqlConnectionStringSupportRequest { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database for validations
        /// </summary>
        [DisplayName("ValidationSqlServer")]
        [DefaultValue("ValidationSqlServer")]
        public string SqlConnectionStringValidation { get; set; }

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
        /// Gets the Application Insights heartbeat interval in seconds associated with this deployment.
        /// </summary>
        public int AppInsightsHeartbeatIntervalSeconds { get; set; }

        /// <summary>
        /// Gets the protocol-independent site root
        /// </summary>
        public string SiteRoot { get; set; }

        /// <summary>
        /// Gets the protocol-independent support email site root
        /// </summary>
        public string SupportEmailSiteRoot { get; set; }

        /// <summary>
        /// Private key for verifying recaptcha user response.
        /// </summary>
        public string ReCaptchaPrivateKey { get; set; }

        /// <summary>
        /// Public key for verifying recaptcha user response.
        /// </summary>
        public string ReCaptchaPublicKey { get; set; }

        /// <summary>
        /// Gets the Google Analytics Property ID being used, if any.
        /// </summary>
        public string GoogleAnalyticsPropertyId { get; set; }

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
        /// Gets a string indicating which Microsoft Entra tenant ID should be used for administrators. 
        /// When specified, the gallery will ensure admin users are logging in using only the specified tenant ID.
        /// Blank means any Microsoft Entra tenant ID can be used by administrators.
        /// </summary>
        public string EnforcedTenantIdForAdmin { get; set; }

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
        /// Defines a semi-colon separated list of domains for the alternate site roots for gallery, used for MSA authentication by AADv2
        /// </summary>
        public string AlternateSiteRootList { get; set; }

        /// <summary>
        /// Configuration to enable manual setting of the machine key for session persistence across deployments/slots.
        /// </summary>
        public bool EnableMachineKeyConfiguration { get; set; }

        /// <summary>
        /// Gets/sets the encryption aglorithm that is used for encrypting and decrypting forms authentication data.
        /// </summary>
        public string MachineKeyDecryption { get; set; }

        /// <summary>
        /// Gets/sets the key that is sued to encrypt and decrypt data, or the process by which the key is generated.
        /// </summary>
        public string MachineKeyDecryptionKey { get; set; }

        /// <summary>
        /// Gets/sets the hashing algorithm used for validating forms authentication and view state data.
        /// </summary>
        public string MachineKeyValidationAlgorithm { get; set; }

        /// <summary>
        /// Gets/sets the key that is used to validate forms authentication and view state data, or the process by which the key is generated.
        /// </summary>
        public string MachineKeyValidationKey { get; set; }

        /// <summary>
        /// Gets/sets a bool that indicates if the OData requests will be filtered.
        /// </summary>
        public bool IsODataFilterEnabled { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to an external status page
        /// </summary>
        public string ExternalStatusUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to an external about page
        /// </summary>
        public string ExternalAboutUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to an external privacy policy
        /// </summary>
        public string ExternalPrivacyPolicyUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to an external terms of use document
        /// </summary>
        public string ExternalTermsOfUseUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to the brand
        /// </summary>
        public string ExternalBrandingUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is brand string to display in the footer, this also
        /// accepts a single {0} string format token which is replaced by the UTC year
        /// </summary>
        public string ExternalBrandingMessage
        {
            get
            {
                return _ExternalBrandingMessage;
            }

            set
            {
                _ExternalBrandingMessage = string.Format(value, DateTime.UtcNow.Year);
            }
        }

        /// <summary>
        /// Get/Sets a string to a url that details trademarks. If unset, the link will not appear.
        /// </summary>
        public string TrademarksUrl { get; set; }

        /// <summary>
        /// Gets/Sets a flag indicating if default security policies should be enforced.
        /// </summary>
        public bool EnforceDefaultSecurityPolicies { get; set; }

        [DefaultValue(true)]
        public bool IsHosted { get; set; }

        public bool RejectSignedPackagesWithNoRegisteredCertificate { get; set; }

        public bool RejectPackagesWithTooManyPackageEntries { get; set; }

        public bool BlockSearchEngineIndexing { get; set; }

        public bool AsynchronousEmailServiceEnabled { get; set; }

        public bool AsynchronousDeleteAccountServiceEnabled { get; set; }

        [DefaultValue(false)]
        public bool BlockLegacyLicenseUrl { get; set; }

        [DefaultValue(true)]
        public bool AllowLicenselessPackages { get; set; }

        public Uri SearchServiceUriPrimary { get; set; }

        public Uri SearchServiceUriSecondary { get; set; }

        public Uri PreviewSearchServiceUriPrimary { get; set; }

        public Uri PreviewSearchServiceUriSecondary { get; set; }

        [DefaultValue(600)]
        public int SearchCircuitBreakerDelayInSeconds { get; set; }

        // The default value was chosen to have searchRetryCount*retryInterval to be close to 1 second in order to keep the user still engaged.
        // https://www.nngroup.com/articles/website-response-times/
        [DefaultValue(500)]
        public int SearchCircuitBreakerWaitAndRetryIntervalInMilliseconds { get; set; }

        [DefaultValue(3)]
        public int SearchCircuitBreakerWaitAndRetryCount { get; set; }

        // Default value was chosen using the AI data.
        // It is the average of the search request count per second during the last 90 days.
        [DefaultValue(200)]
        public int SearchCircuitBreakerBreakAfterCount { get; set; }

        /// <summary>
        /// We use the default of HttpClient.Timeout, which is is 100 seconds == 100000 milliseconds.
        /// </summary>
        [DefaultValue(100000)]
        public int SearchHttpRequestTimeoutInMilliseconds { get; set; }

        [DefaultValue("")]
        public string EmbeddedIconUrlTemplate { get; set; }

        [DefaultValue(null)]
        public string DeploymentLabel { get; set; }

        [DefaultValue(null)]
        public int? MinWorkerThreads { get; set; }
        [DefaultValue(null)]
        public int? MaxWorkerThreads { get; set; }
        [DefaultValue(null)]
        public int? MinIoThreads { get; set; }
        [DefaultValue(null)]
        public int? MaxIoThreads { get; set; }
        public string InternalMicrosoftTenantKey { get; set; }
        public string AdminSenderUser { get; set; }
        [DefaultValue(16 * 1024 * 1024)]
        public int MaxJsonLengthOverride { get; set; }
    }
}
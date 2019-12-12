// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Configuration
{
    public interface IAppConfiguration : IMessageServiceConfiguration
    {
        /// <summary>
        /// Gets the location in which the Lucene Index is stored
        /// </summary>
        LuceneIndexLocation LuceneIndexLocation { get; set; }

        /// <summary>
        /// Gets the name of the environment in which the gallery is deployed
        /// </summary>
        string Environment { get; set; }

        /// <summary>
        /// Gets the warning banner text 
        /// </summary>
        string WarningBanner { get; set; }

        /// <summary>
        /// Gets a setting indicating if SSL is required for all operations once logged in.
        /// </summary>
        bool RequireSSL { get; set; }

        /// <summary>
        /// Gets the port used for SSL
        /// </summary>
        int SSLPort { get; set; }

        /// <summary>
        /// A string containing a path exluded from forcing the HTTP to HTTPS redirection.
        /// To provide multiple paths separate them with ;
        /// </summary>
        /// <example>/api/health-probe</example>
        string[] ForceSslExclusion { get; set; }

        /// <summary>
        /// The Azure Storage connection string used for auditing.
        /// </summary>
        string AzureStorage_Auditing_ConnectionString { get; set; }

        /// <summary>
        /// The Azure Storage connection string used for user certificates.
        /// </summary>
        string AzureStorage_UserCertificates_ConnectionString { get; set; }

        /// <summary>
        /// The Azure Storage connection string used for static content.
        /// </summary>
        string AzureStorage_Content_ConnectionString { get; set; }

        /// <summary>
        /// The Azure Storage connection string used for Elmah error logs.
        /// </summary>
        string AzureStorage_Errors_ConnectionString { get; set; }

        /// <summary>
        /// The Azure Storage connection string used for packages, after upload.
        /// </summary>
        string AzureStorage_Packages_ConnectionString { get; set; }

        /// <summary>
        /// The Azure Storage connection string used for flatContainer, after upload.
        /// </summary>
        string AzureStorage_FlatContainer_ConnectionString { get; set; }

        /// <summary>
        /// The Azure Storage connection string used for statistics.
        /// </summary>
        string AzureStorage_Statistics_ConnectionString { get; set; }

        /// <summary>
        /// The Azure Storage connection string used for package uploads, before publishing.
        /// </summary>
        string AzureStorage_Uploads_ConnectionString { get; set; }

        /// <summary>
        /// The Azure storage connection string used for RevalidateCertificate job admin panel.
        /// </summary>
        string AzureStorage_Revalidation_ConnectionString { get; set; }

        /// <summary>
        /// Gets a setting if Read Access Geo Redundant is enabled in azure storage
        /// </summary>
        bool AzureStorageReadAccessGeoRedundant { get; set; }

        /// <summary>
        /// How frequently the feature flags should be refreshed.
        /// </summary>
        TimeSpan FeatureFlagsRefreshInterval { get; set; }

        /// <summary>
        /// Gets a boolean indicating whether DB admin through web UI should be accesible.
        /// </summary>
        bool AdminPanelDatabaseAccessEnabled { get; set; }

        /// <summary>
        /// Gets a boolean indicating whether asynchronous package validation is enabled.
        /// </summary>
        bool AsynchronousPackageValidationEnabled { get; set; }

        /// <summary>
        /// Only makes sense if <see cref="AsynchronousPackageValidationEnabled"/> is set to true.
        /// Indicates whether async package validation will be run in blocking mode.
        /// Running in blocking mode means that the package will not be available for download
        /// until it successfully passed all validations.
        /// </summary>
        bool BlockingAsynchronousPackageValidationEnabled { get; set; }

        /// <summary>
        /// If <see cref="AsynchronousPackageValidationEnabled"/> is set to true,
        /// this is the delay that downstream validations should wait before starting
        /// to process a package.
        /// </summary>
        TimeSpan AsynchronousPackageValidationDelay { get; set; }

        /// <summary>
        /// The upper bound for package validations. A notice will be displayed if a package's validation exceeds this value.
        /// </summary>
        TimeSpan ValidationExpectedTime { get; set; }

        /// <summary>
        /// Gets a boolean indicating whether NuGet password logins are deprecated.
        /// </summary>
        bool DeprecateNuGetPasswordLogins { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the site requires that email addresses be confirmed
        /// </summary>
        bool ConfirmEmailAddresses { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the site is in read only mode
        /// </summary>
        bool ReadOnlyMode { get; set; }

        /// <summary>
        /// Gets if only service feeds should be registered
        /// </summary>
        bool FeedOnlyMode { get; set; }

        /// <summary>
        /// Gets the local directory in which to store files.
        /// </summary>
        string FileStorageDirectory { get; set; }

        /// <summary>
        /// Gets the site brand name i.e. 'NuGet Gallery' by default. Cobranding feature.
        /// </summary>
        string Brand { get; set; }

        /// <summary>
        /// Gets the storage mechanism used by this instance of the gallery
        /// </summary>
        string StorageType { get; set; }

        /// <summary>
        /// Gets the URI of the SMTP host to use. Or null if SMTP is not being used. Use <see cref="NuGetGallery.Configuration.SmtpUri"/> to parse it
        /// </summary>
        Uri SmtpUri { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database
        /// </summary>
        string SqlConnectionString { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database read only replica.
        /// </summary>
        string SqlReadOnlyReplicaConnectionString { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database for support requests
        /// </summary>
        string SqlConnectionStringSupportRequest { get; set; }

        /// <summary>
        /// Gets the SQL Connection string used to connect to the database for validations
        /// </summary>
        string SqlConnectionStringValidation { get; set; }

        /// <summary>
        /// Gets the host name of the Azure CDN being used
        /// </summary>
        string AzureCdnHost { get; set; }

        /// <summary>
        /// Gets the App ID of the Facebook app associated with this deployment
        /// </summary>
        string FacebookAppId { get; set; }

        /// <summary>
        /// Gets the Application Insights instrumentation key associated with this deployment.
        /// </summary>
        string AppInsightsInstrumentationKey { get; set; }

        /// <summary>
        /// Gets the Application Insights sampling percentage associated with this deployment.
        /// </summary>
        double AppInsightsSamplingPercentage { get; set; }

        /// <summary>
        /// Gets the Application Insights heartbeat interval in seconds associated with this deployment.
        /// </summary>
        int AppInsightsHeartbeatIntervalSeconds { get; set; }

        /// <summary>
        /// Gets the protocol-independent site root
        /// </summary>
        string SiteRoot { get; set; }

        /// <summary>
        /// Private key for verifying recaptcha user response.
        /// </summary>
        string ReCaptchaPrivateKey { get; set; }

        /// <summary>
        /// Public key for verifying recaptcha user response.
        /// </summary>
        string ReCaptchaPublicKey { get; set; }

        /// <summary>
        /// Gets the Google Analytics Property ID being used, if any.
        /// </summary>
        string GoogleAnalyticsPropertyId { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the search index should be updated automatically in the background
        /// </summary>
        bool AutoUpdateSearchIndex { get; set; }

        /// <summary>
        /// Gets a string indicating which authentication provider(s) are supported for administrators. 
        /// When specified, the gallery will ensure admin users are logging in using any of the specified authentication providers.
        /// Blank means any authentication provider can be used by administrators.
        /// </summary>
        string EnforcedAuthProviderForAdmin { get; set; }

        /// <summary>
        /// Gets a string indicating which AAD Tenant Id should be used for administrators. 
        /// When specified, the gallery will ensure admin users are logging in using only the specified tenant ID.
        /// Blank means any AAD tenant ID can be used by administrators.
        /// </summary>
        string EnforcedTenantIdForAdmin { get; set; }

        /// <summary>
        /// The required format for a user password.
        /// </summary>
        string UserPasswordRegex { get; set; }

        /// <summary>
        /// A message to show the user, to explain password requirements.
        /// </summary>
        string UserPasswordHint { get; set; }

        /// <summary>
        /// Defines the time after which V1 API keys expire.
        /// </summary>
        int ExpirationInDaysForApiKeyV1 { get; set; }

        /// <summary>
        /// Defines the number of days before the API key expires when the server should emit a warning to the client.
        /// </summary>
        int WarnAboutExpirationInDaysForApiKeyV1 { get; set; }

        /// <summary>
        /// Defines a semi-colon separated list of domains for the alternate site root for gallery, used for MSA authentication by AADv2
        /// </summary>
        string AlternateSiteRootList { get; set; }

        /// <summary>
        /// Configuration to enable manual setting of the machine key for session persistence across deployments/slots.
        /// </summary>
        bool EnableMachineKeyConfiguration { get; set; }

        /// <summary>
        /// Defines the encryption aglorithm that is used for encrypting and decrypting forms authentication data.
        /// </summary>
        string MachineKeyDecryption { get; set; }

        /// <summary>
        /// Defines the key that is sued to encrypt and decrypt data, or the process by which the key is generated.
        /// </summary>
        string MachineKeyDecryptionKey { get; set; }

        /// <summary>
        /// Defines the hashing algorithm used for validating forms authentication and view state data.
        /// </summary>
        string MachineKeyValidationAlgorithm { get; set; }

        /// <summary>
        /// Defines the key that is used to validate forms authentication and view state data, or the process by which the key is generated.
        /// </summary>
        string MachineKeyValidationKey { get; set; }

        /// <summary>
        /// Gets/sets a bool that indicates if the OData requests will be filtered.
        /// </summary>
        bool IsODataFilterEnabled { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to the status page
        /// </summary>
        string ExternalStatusUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to an external about page
        /// </summary>
        string ExternalAboutUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to an external privacy policy
        /// </summary>
        string ExternalPrivacyPolicyUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to an external terms of use document
        /// </summary>
        string ExternalTermsOfUseUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is a link to the brand
        /// </summary>
        string ExternalBrandingUrl { get; set; }

        /// <summary>
        /// Gets/sets a string that is brand string to display in the footer, this also
        /// accepts a single {0} string format token which is replaced by the UTC year
        /// </summary>
        string ExternalBrandingMessage { get; set; }

        /// <summary>
        /// Get/Sets a string to a url that details trademarks. If unset, the link will not appear.
        /// </summary>
        string TrademarksUrl { get; set; }

        /// <summary>
        /// Gets/Sets a flag indicating if default security policies should be enforced.
        /// </summary>
        bool EnforceDefaultSecurityPolicies { get; set; }

        /// <summary>
        /// Whether or not the gallery is running as a hosted web service. This should always be true unless the
        /// gallery code is being used inside a console application.
        /// </summary>
        bool IsHosted { get; set; }

        /// <summary>
        /// Whether or not to synchronously reject signed packages on push/upload when no certificate is uploaded
        /// by the owner.
        /// </summary>
        bool RejectSignedPackagesWithNoRegisteredCertificate { get; set; }

        /// <summary>
        /// Whether or not to synchronously reject packages on push/upload that have too many package entries.
        /// </summary>
        bool RejectPackagesWithTooManyPackageEntries { get; set; }

        /// <summary>
        /// Whether or not to block search engines from indexing the web pages using the "noindex" meta tag.
        /// </summary>
        bool BlockSearchEngineIndexing { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether asynchronous email service is enabled.
        /// </summary>
        bool AsynchronousEmailServiceEnabled { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether asynchronous account deletion service is enabled.
        /// </summary>
        bool AsynchronousDeleteAccountServiceEnabled { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating if this gallery allows users to delete their own account.
        /// </summary>
        bool SelfServiceAccountDeleteEnabled { get; set; }

        /// <summary>
        /// Indicates whether packages that specify the license the "old" way (with a "licenseUrl" node only) should be rejected.
        /// </summary>
        bool BlockLegacyLicenseUrl { get; set; }

        /// <summary>
        /// Indicates whether packages that don't specify any license information (no license URL, no license expression,
        /// no embedded license) are allowed into Gallery.
        /// </summary>
        bool AllowLicenselessPackages { get; set; }

        /// <summary>
        /// The URL for the primary search endpoint, for stable behavior.
        /// </summary>
        Uri SearchServiceUriPrimary { get; set; }

        /// <summary>
        /// The URL for the secondary search endpoint, for stable behavior.
        /// </summary>
        Uri SearchServiceUriSecondary { get; set; }

        /// <summary>
        /// The URL for the primary search endpoint, for preview behavior.
        /// </summary>
        Uri PreviewSearchServiceUriPrimary { get; set; }

        /// <summary>
        /// The URL for the secondary search endpoint, for preview behavior.
        /// </summary>
        Uri PreviewSearchServiceUriSecondary { get; set; }

        /// <summary>
        /// The time in seconds for the circuit breaker delay. (The time the circuit breaker will stay in open state)
        /// </summary>
        int SearchCircuitBreakerDelayInSeconds { get; set; }

        /// <summary>
        /// The wait time in milliseconds for the WaitAndRetry policy.
        /// </summary>
        int SearchCircuitBreakerWaitAndRetryIntervalInMilliseconds { get; set; }

        /// <summary>
        /// A request will fail after this number of retries. In total a request will fail after this number of retries + 1.
        /// </summary>
        int SearchCircuitBreakerWaitAndRetryCount { get; set; }

        /// <summary>
        /// CircuitBreaker will open after this number of consecutive failed requests.
        /// </summary>
        int SearchCircuitBreakerBreakAfterCount { get; set; }

        /// <summary>
        /// The Search timeout per request in milliseconds.
        /// </summary>
        int SearchHttpRequestTimeoutInMilliseconds { get; set; }

        /// <summary>
        /// Template for the storage URL for packages with embedded icons.
        /// When expanded the '{id-lower}' will be replaced with the package id in lowercase,
        /// '{version-lower}' will be replaced with the normalized package version in lowercase.
        /// </summary>
        string EmbeddedIconUrlTemplate { get; set; }

        /// <summary>
        /// Deployment label to log with telemetry.
        /// </summary>
        string DeploymentLabel { get; set; }

        /// <summary>
        /// The Usabilla feedback button ID embedded in the JavaScript snippet obtained from Usabilla. The ID can found
        /// in your button's JavaScript code. Look for "//w.usabilla.com/{button ID}.js".
        /// </summary>
        string UsabillaFeedbackButtonId { get; set; }
    }
}
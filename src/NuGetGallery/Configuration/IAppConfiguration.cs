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
        /// The Azure storage connection string used for feature flags.
        /// </summary>
        string AzureStorage_FeatureFlags_ConnectionString { get; set; }

        /// <summary>
        /// Gets a setting if Read Access Geo Redundant is enabled in azure storage
        /// </summary>
        bool AzureStorageReadAccessGeoRedundant { get; set; }

        /// <summary>
        /// How frequently the feature flags should be refreshed.
        /// </summary>
        TimeSpan FeatureFlagsRefreshInterval { get; set; }

        /// <summary>
        /// The maximum refresh staleness allowed for feature flags.
        /// If the threshold is reached, the returned feature flags will be labeled as stale.
        /// </summary>
        TimeSpan FeatureFlagsMaximumStaleness { get; set; }

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
        /// Gets the URI to the search service
        /// </summary>
        Uri ServiceDiscoveryUri { get; set; }

        /// <summary>
        /// Gets the @type for the Search endpoint
        /// </summary>
        string SearchServiceResourceType { get; set; }

        /// <summary>
        /// Gets the @type for the Autocomplete endpoint
        /// </summary>
        string AutocompleteServiceResourceType { get; set; }

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
        /// Gets a boolean indicating if perf logs should be collected
        /// </summary>
        bool CollectPerfLogs { get; set; }

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
        /// The name of zero or more curated feeds that are redirected to the main feed.
        /// </summary>
        string[] RedirectedCuratedFeeds { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether asynchronous email service is enabled.
        /// </summary>
        bool AsynchronousEmailServiceEnabled { get; set; }

        /// <summary>
        /// Flag that indicates whether packages with `license` node in them should be rejected.
        /// </summary>
        bool RejectPackagesWithLicense { get; set; }

        /// <summary>
        /// Indicates whether packages that specify the license the "old" way (with a "licenseUrl" node only) should be rejected.
        /// </summary>
        bool BlockLegacyLicenseUrl { get; set; }

        /// <summary>
        /// Indicates whether packages that don't specify any license information (no license URL, no license expression,
        /// no embedded license) are allowed into Gallery.
        /// </summary>
        bool AllowLicenselessPackages { get; set; }
    }
}
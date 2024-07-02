// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using NuGetGallery.Configuration;

namespace NuGetGallery.AccountDeleter
{
    public class GalleryConfiguration : IAppConfiguration
    {
        public string SiteRoot
        {
            get
            {
                return "";
            }

            set => throw new NotImplementedException();
        }

        public LuceneIndexLocation LuceneIndexLocation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Environment { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string WarningBanner { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool RequireSSL { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SSLPort { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string[] ForceSslExclusion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureStorage_Auditing_ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureStorage_UserCertificates_ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureStorage_Content_ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureStorage_Packages_ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureStorage_FlatContainer_ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureStorage_Statistics_ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureStorage_Statistics_ConnectionString_Alternate { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureStorage_Uploads_ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureStorage_Revalidation_ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool AzureStorageReadAccessGeoRedundant { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeSpan FeatureFlagsRefreshInterval { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool AdminPanelEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool AdminPanelDatabaseAccessEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool AsynchronousPackageValidationEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool BlockingAsynchronousPackageValidationEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeSpan AsynchronousPackageValidationDelay { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeSpan ValidationExpectedTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool DeprecateNuGetPasswordLogins { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool ConfirmEmailAddresses { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool ReadOnlyMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool FeedOnlyMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FileStorageDirectory { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Brand { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string StorageType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Uri SmtpUri { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string SqlConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string SqlConnectionStringSupportRequest { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string SqlConnectionStringValidation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AzureCdnHost { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FacebookAppId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AppInsightsInstrumentationKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double AppInsightsSamplingPercentage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int AppInsightsHeartbeatIntervalSeconds { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ReCaptchaPrivateKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ReCaptchaPublicKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string GoogleAnalyticsPropertyId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool CollectPerfLogs { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool AutoUpdateSearchIndex { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string EnforcedAuthProviderForAdmin { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string EnforcedTenantIdForAdmin { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string UserPasswordRegex { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string UserPasswordHint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int ExpirationInDaysForApiKeyV1 { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int WarnAboutExpirationInDaysForApiKeyV1 { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AlternateSiteRootList { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool EnableMachineKeyConfiguration { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string MachineKeyDecryption { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string MachineKeyDecryptionKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string MachineKeyValidationAlgorithm { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string MachineKeyValidationKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsODataFilterEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ExternalStatusUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ExternalAboutUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ExternalPrivacyPolicyUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ExternalTermsOfUseUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ExternalBrandingUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ExternalBrandingMessage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string TrademarksUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool EnforceDefaultSecurityPolicies { get => true; set => throw new NotImplementedException(); }
        public bool IsHosted { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool RejectSignedPackagesWithNoRegisteredCertificate { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool RejectPackagesWithTooManyPackageEntries { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool BlockSearchEngineIndexing { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool AsynchronousEmailServiceEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool RejectPackagesWithLicense { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool BlockLegacyLicenseUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool AllowLicenselessPackages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Uri SearchServiceUriPrimary { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Uri SearchServiceUriSecondary { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Uri PreviewSearchServiceUriPrimary { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Uri PreviewSearchServiceUriSecondary { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SearchCircuitBreakerDelayInSeconds { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SearchCircuitBreakerWaitAndRetryIntervalInMilliseconds { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SearchCircuitBreakerWaitAndRetryCount { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SearchCircuitBreakerBreakAfterCount { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SearchHttpRequestTimeoutInMilliseconds { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public MailAddress GalleryOwner { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public MailAddress GalleryNoReplyAddress { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string SqlReadOnlyReplicaConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool AsynchronousDeleteAccountServiceEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string EmbeddedIconUrlTemplate { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool SelfServiceAccountDeleteEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string DeploymentLabel { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int? MinWorkerThreads { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int? MaxWorkerThreads { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int? MinIoThreads { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int? MaxIoThreads { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string InternalMicrosoftTenantKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string AdminSenderUser { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string SupportEmailSiteRoot { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int MaxJsonLengthOverride { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}

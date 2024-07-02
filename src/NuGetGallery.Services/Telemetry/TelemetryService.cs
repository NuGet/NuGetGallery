// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Web;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;
using NuGet.Versioning;
using NuGetGallery.Authentication;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    public class TelemetryService : ITelemetryService, IFeatureFlagTelemetryService
    {
        public static class Events
        {
            public const string ODataQueryFilter = "ODataQueryFilter";
            public const string ODataCustomQuery = "ODataCustomQuery";
            public const string PackagePush = "PackagePush";
            public const string PackagePushFailure = "PackagePushFailure";
            public const string CreatePackageVerificationKey = "CreatePackageVerificationKey";
            public const string VerifyPackageKey = "VerifyPackageKey";
            public const string PackageReadMeChanged = "PackageReadMeChanged";
            public const string PackagePushNamespaceConflict = "PackagePushNamespaceConflict";
            public const string PackagePushOwnerlessNamespaceConflict = "PackagePushOwnerlessNamespaceConflict";
            public const string NewUserRegistration = "NewUserRegistration";
            public const string CredentialAdded = "CredentialAdded";
            public const string CredentialUsed = "CredentialUsed";
            public const string DownloadCountDecreasedDuringRefresh = "DownloadCountDecreasedDuringRefresh";
            public const string DownloadJsonRefreshDuration = "DownloadJsonRefreshDuration";
            public const string GalleryDownloadGreaterThanJsonForPackage = "GalleryDownloadGreaterThanJsonForPackage";
            public const string GalleryDownloadGreaterThanJsonForPackageRegistration = "GalleryDownloadGreaterThanJsonForPackageRegistration";
            public const string GetPackageDownloadCountFailed = "GetPackageDownloadCountFailed";
            public const string GetPackageRegistrationDownloadCountFailed = "GetPackageRegistrationDownloadCountFailed";
            public const string DownloadJsonTotalPackageIds = "DownloadJsonTotalPackageIds";
            public const string DownloadJsonTotalPackageVersions = "DownloadJsonTotalPackageVersions";
            public const string UserPackageDeleteCheckedAfterHours = "UserPackageDeleteCheckedAfterHours";
            public const string UserPackageDeleteExecuted = "UserPackageDeleteExecuted";
            public const string UserMultiFactorAuthenticationEnabled = "UserMultiFactorAuthenticationEnabled";
            public const string UserMultiFactorAuthenticationDisabled = "UserMultiFactorAuthenticationDisabled";
            public const string PackageReflow = "PackageReflow";
            public const string PackageUnlisted = "PackageUnlisted";
            public const string PackageListed = "PackageListed";
            public const string PackagesUpdateListed = "PackagesUpdateListed";
            public const string PackageDelete = "PackageDelete";
            public const string PackageDeprecate = "PackageDeprecate";
            public const string PackageReupload = "PackageReupload";
            public const string PackageHardDeleteReflow = "PackageHardDeleteReflow";
            public const string PackageRevalidate = "PackageRevalidate";
            public const string OrganizationTransformInitiated = "OrganizationTransformInitiated";
            public const string OrganizationTransformCompleted = "OrganizationTransformCompleted";
            public const string OrganizationTransformDeclined = "OrganizationTransformDeclined";
            public const string OrganizationTransformCancelled = "OrganizationTransformCancelled";
            public const string OrganizationAdded = "OrganizationAdded";
            public const string CertificateAdded = "CertificateAdded";
            public const string CertificateActivated = "CertificateActivated";
            public const string CertificateDeactivated = "CertificateDeactivated";
            public const string PackageRegistrationRequiredSignerSet = "PackageRegistrationRequiredSignerSet";
            public const string AccountDeleteCompleted = "AccountDeleteCompleted";
            public const string AccountDeleteRequested = "AccountDeleteRequested";
            public const string SymbolPackagePush = "SymbolPackagePush";
            public const string SymbolPackageDelete = "SymbolPackageDelete";
            public const string SymbolPackagePushFailure = "SymbolPackagePushFailure";
            public const string SymbolPackageGalleryValidation = "SymbolPackageGalleryValidation";
            public const string SymbolPackageRevalidate = "SymbolPackageRevalidate";
            public const string PackageMetadataComplianceError = "PackageMetadataComplianceError";
            public const string PackageMetadataComplianceWarning = "PackageMetadataComplianceWarning";
            public const string PackageOwnershipAutomaticallyAdded = "PackageOwnershipAutomaticallyAdded";
            public const string TyposquattingCheckResultAndTotalTimeInMs = "TyposquattingCheckResultAndTotalTimeInMs";
            public const string TyposquattingChecklistRetrievalTimeInMs = "TyposquattingChecklistRetrievalTimeInMs";
            public const string TyposquattingAlgorithmProcessingTimeInMs = "TyposquattingAlgorithmProcessingTimeInMs";
            public const string TyposquattingOwnersCheckTimeInMs = "TyposquattingOwnersCheckTimeInMs";
            public const string InvalidLicenseMetadata = "InvalidLicenseMetadata";
            public const string NonFsfOsiLicenseUsed = "NonFsfOsiLicenseUsed";
            public const string LicenseFileRejected = "LicenseFileRejected";
            public const string LicenseValidationFailed = "LicenseValidationFailed";
            public const string FeatureFlagStalenessSeconds = "FeatureFlagStalenessSeconds";
            public const string SearchExecutionDuration = "SearchExecutionDuration";
            public const string SearchCircuitBreakerOnBreak = "SearchCircuitBreakerOnBreak";
            public const string SearchCircuitBreakerOnReset = "SearchCircuitBreakerOnReset";
            public const string SearchOnRetry = "SearchOnRetry";
            public const string SearchOnTimeout = "SearchOnTimeout";
            public const string SearchSideBySideFeedback = "SearchSideBySideFeedback";
            public const string SearchSideBySide = "SearchSideBySide";
            public const string ABTestEnrollmentInitialized = "ABTestEnrollmentInitialized";
            public const string ABTestEnrollmentUpgraded = "ABTestEnrollmentUpgraded";
            public const string ABTestEvaluated = "ABTestEvaluated";
            public const string PackagePushDisconnect = "PackagePushDisconnect";
            public const string SymbolPackagePushDisconnect = "SymbolPackagePushDisconnect";
            public const string VulnerabilitiesCacheRefreshDurationMs = "VulnerabilitiesCacheRefreshDurationMs";
            public const string InstanceUptime = "InstanceUptimeInDays";
            public const string ApiRequest = "ApiRequest";
            public const string CreateSqlConnectionDurationMs = "CreateSqlConnectionDurationMs";
        }

        private readonly IDiagnosticsSource _diagnosticsSource;
        private readonly ITelemetryClient _telemetryClient;

        private readonly JsonSerializerSettings _defaultJsonSerializerSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.None
        };

        // Download Count properties
        public const string OldJsonDownloadCount = "OldDownloadCount";
        public const string NewJsonDownloadCount = "NewDownloadCount";
        public const string GalleryDownloadCount = "GalleryDownloadCount";
        public const string JsonDownloadCount = "JsonDownloadCount";

        // ODataQueryFilter properties
        public const string CallContext = "CallContext";
        public const string IsEnabled = "IsEnabled";
        public const string IsAllowed = "IsAllowed";
        public const string QueryPattern = "QueryPattern";

        // ODataCustomQuery properties
        public const string IsCustomQuery = "IsCustomQuery";

        // Package event properties
        public const string AuthenticationMethod = "AuthenticationMethod";
        public const string ClientVersion = "ClientVersion";
        public const string ProtocolVersion = "ProtocolVersion";
        public const string ClientInformation = "ClientInformation";
        public const string UserAgent = "UserAgent";
        public const string IsAuthenticated = "IsAuthenticated";
        public const string IsScoped = "IsScoped";
        public const string KeyCreationDate = "KeyCreationDate";
        public const string PackageId = "PackageId";
        public const string PackageVersion = "PackageVersion";
        public const string PackageVersions = "PackageVersions";

        // Package listed properties
        public const string Listed = "Listed";

        // Package deprecate properties
        public const string DeprecationReason = "PackageDeprecationReason";
        public const string DeprecationAlternatePackageId = "PackageDeprecationAlternatePackageId";
        public const string DeprecationAlternatePackageVersion = "PackageDeprecationAlternatePackageVersion";
        public const string DeprecationCustomMessage = "PackageDeprecationCustomMessage";
        public const string DeprecationHasChanges = "PackageDeprecationHasChanges";

        // User properties
        public const string RegistrationMethod = "RegistrationMethod";
        public const string AccountCreationDate = "AccountCreationDate";
        public const string WasMultiFactorAuthenticated = "WasMultiFactorAuthenticated";

        // Verify package properties
        public const string IsVerificationKeyUsed = "IsVerificationKeyUsed";
        public const string VerifyPackageKeyStatusCode = "VerifyPackageKeyStatusCode";

        // Package ReadMe properties
        public const string ReadMeSourceType = "ReadMeSourceType";
        public const string ReadMeState = "ReadMeState";

        // User package delete checked properties
        public const string Outcome = "Outcome";
        public const string PackageKey = "PackageKey";
        public const string IdDatabaseDownloads = "IdDatabaseDownloads";
        public const string IdReportDownloads = "IdReportDownloads";
        public const string VersionDatabaseDownloads = "VersionDatabaseDownloads";
        public const string VersionReportDownloads = "VersionReportDownloads";
        public const string ReportPackageReason = "ReportPackageReason";
        public const string PackageDeleteDecision = "PackageDeleteDecision";

        // User package delete executed properties
        public const string Success = "Success";

        // Package delete properties
        public const string IsHardDelete = "IsHardDelete";

        // Organization properties
        public const string OrganizationAccountKey = "OrganizationAccountKey";
        public const string OrganizationIsRestrictedToOrganizationTenantPolicy = "OrganizationIsRestrictedToOrganizationTenantPolicy";

        // Certificate properties
        public const string Sha256Thumbprint = "Sha256Thumbprint";

        //Account Delete Properties
        public const string AccountDeletedByRole = "AccountDeletedByRole";
        public const string AccountIsSelfDeleted = "AccountIsSelfDeleted";
        public const string AccountDeletedIsOrganization = "AccountDeletedIsOrganization";
        public const string CreatedDateForAccountToBeDeleted = "CreatedDateForAccountToBeDeleted";
        public const string AccountDeleteSucceeded = "AccountDeleteSucceeded";

        // Package metadata compliance properties
        public const string ComplianceFailures = "ComplianceFailures";
        public const string ComplianceWarnings = "ComplianceWarnings";

        public const string ValueUnknown = "Unknown";

        // Typosquatting check properties
        private const int TyposquattingCollisionIdsMaxPropertyValue = 10;
        public const string WasUploadBlocked = "WasUploadBlocked";
        public const string CollisionPackageIds = "CollisionPackageIds";
        public const string CollisionPackageIdsCount = "CollisionPackageIdsCount";
        public const string CheckListLength = "CheckListLength";
        public const string HasExtraCollisionPackageIds = "HasExtraCollisionPackageIds";
        public const string CheckListCacheExpireTimeInHours = "CheckListCacheExpireTimeInHours";

        // License related properties
        public const string LicenseExpression = "LicenseExpression";

        // Search related properties
        public const string SearchUrl = "SearchUrl";
        public const string SearchHttpResponseCode = "SearchHttpResponseCode";
        public const string SearchSuccessExecutionStatus = "SearchSuccessExecutionStatus";
        public const string SearchException = "SearchException";
        public const string SearchName = "SearchName";
        public const string SearchPollyCorrelationId = "SearchPollyCorrelationId";
        public const string SearchCircuitBreakerStatus = "SearchCircuitBreakerStatus";

        // Search side-by-side properties
        public const string SearchTerm = "SearchTerm";
        public const string OldHits = "OldHits";
        public const string OldSuccess = "OldSuccess";
        public const string NewHits = "NewHits";
        public const string NewSuccess = "NewSuccess";
        public const string BetterSide = "BetterSide";
        public const string MostRelevantPackage = "MostRelevantPackage";
        public const string ExpectedPackages = "ExpectedPackages";
        public const string HasComments = "HasComments";
        public const string HasEmailAddress = "HasEmailAddress";

        // A/B testing properties
        public const string OldSchemaVersion = "OldSchemaVersion";
        public const string NewSchemaVersion = "NewSchemaVersion";
        public const string PreviewSearchBucket = "PreviewSearchBucket";
        public const string PackageDependentBucket = "PackageDependentBucket";
        public const string TestName = "TestName";
        public const string IsActive = "IsActive";
        public const string TestBucket = "TestBucket";
        public const string TestPercentage = "TestPercentage";

        public const string Endpoint = "Endpoint";

        public const string Kind = "Kind";
        public const string Sync = "Sync";
        public const string Async = "Async";

        public TelemetryService(IDiagnosticsSource diagnosticsSource, ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _diagnosticsSource = diagnosticsSource ?? throw new ArgumentNullException(nameof(diagnosticsSource));
        }

        public void TraceException(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            _diagnosticsSource.Warning(exception.ToString());
        }

        public void TrackGetPackageDownloadCountFailed(string packageId, string packageVersion)
        {
            TrackMetric(Events.GetPackageDownloadCountFailed, 1, properties =>
            {
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
            });
        }

        public void TrackGetPackageRegistrationDownloadCountFailed(string packageId)
        {
            TrackMetric(Events.GetPackageRegistrationDownloadCountFailed, 1, properties =>
            {
                properties.Add(PackageId, packageId);
            });
        }

        public void TrackDownloadJsonRefreshDuration(TimeSpan duration)
        {
            TrackMetric(Events.DownloadJsonRefreshDuration, duration.TotalMilliseconds, properties => { });
        }

        public void TrackDownloadJsonTotalPackageIds(int totalPackageIds)
        {
            TrackMetric(Events.DownloadJsonTotalPackageIds, totalPackageIds, properties => { });
        }

        public void TrackDownloadJsonTotalPackageVersions(int totalPackageVersions)
        {
            TrackMetric(Events.DownloadJsonTotalPackageVersions, totalPackageVersions, properties => { });
        }

        public void TrackDownloadCountDecreasedDuringRefresh(string packageId, string packageVersion, long oldCount, long newCount)
        {
            TrackMetric(Events.DownloadCountDecreasedDuringRefresh, 1, properties =>
            {
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                properties.Add(OldJsonDownloadCount, oldCount.ToString());
                properties.Add(NewJsonDownloadCount, newCount.ToString());
            });
        }

        public void TrackPackageDownloadCountDecreasedFromGallery(string packageId, string packageVersion, long galleryCount, long jsonCount)
        {
            TrackMetric(Events.GalleryDownloadGreaterThanJsonForPackage, 1, properties =>
            {
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                properties.Add(GalleryDownloadCount, galleryCount.ToString());
                properties.Add(JsonDownloadCount, jsonCount.ToString());
            });
        }

        public void TrackPackageRegistrationDownloadCountDecreasedFromGallery(string packageId, long galleryCount, long jsonCount)
        {
            TrackMetric(Events.GalleryDownloadGreaterThanJsonForPackageRegistration, 1, properties =>
            {
                properties.Add(PackageId, packageId);
                properties.Add(GalleryDownloadCount, galleryCount.ToString());
                properties.Add(JsonDownloadCount, jsonCount.ToString());
            });
        }

        public void TrackODataQueryFilterEvent(string callContext, bool isEnabled, bool isAllowed, string queryPattern)
        {
            TrackMetric(Events.ODataQueryFilter, 1, properties =>
            {
                properties.Add(CallContext, callContext);
                properties.Add(IsEnabled, $"{isEnabled}");

                properties.Add(IsAllowed, $"{isAllowed}");
                properties.Add(QueryPattern, queryPattern);
            });
        }

        public void TrackODataCustomQuery(bool? customQuery)
        {
            TrackMetric(Events.ODataCustomQuery, 1, properties =>
            {
                properties.Add(IsCustomQuery, customQuery?.ToString() ?? "Unknown");
            });
        }

        public void TrackPackageReadMeChangeEvent(Package package, string readMeSourceType, PackageEditReadMeState readMeState)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (string.IsNullOrWhiteSpace(readMeSourceType))
            {
                throw new ArgumentNullException(nameof(readMeSourceType));
            }

            TrackMetric(Events.PackageReadMeChanged, 1, properties =>
            {
                properties.Add(PackageId, package.PackageRegistration.Id);
                properties.Add(PackageVersion, package.NormalizedVersion);
                properties.Add(ReadMeSourceType, readMeSourceType);
                properties.Add(ReadMeState, Enum.GetName(typeof(PackageEditReadMeState), readMeState));
            });
        }

        public void TrackPackagePushEvent(Package package, User user, IIdentity identity)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            TrackMetricForPackage(Events.PackagePush, package.PackageRegistration.Id, package.NormalizedVersion, user, identity);
        }

        public void TrackPackagePushFailureEvent(string id, NuGetVersion version)
        {
            var normalizedVersion = version?.ToNormalizedString();

            TrackMetric(Events.PackagePushFailure, 1, properties =>
            {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(PackageId, id ?? ValueUnknown);
                properties.Add(PackageVersion, normalizedVersion ?? ValueUnknown);
            });
        }

        public void TrackPackagePushNamespaceConflictEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            TrackMetricForPackage(Events.PackagePushNamespaceConflict, packageId, packageVersion, user, identity);
        }

        public void TrackPackagePushOwnerlessNamespaceConflictEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            TrackMetricForPackage(Events.PackagePushOwnerlessNamespaceConflict, packageId, packageVersion, user, identity);
        }

        public void TrackCreatePackageVerificationKeyEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            TrackMetricForPackage(Events.CreatePackageVerificationKey, packageId, packageVersion, user, identity);
        }

        public void TrackVerifyPackageKeyEvent(string packageId, string packageVersion, User user, IIdentity identity, int statusCode)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            var hasVerifyScope = identity.HasScopeThatAllowsActions(NuGetScopes.PackageVerify).ToString();

            TrackMetric(Events.VerifyPackageKey, 1, properties =>
            {
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                properties.Add(KeyCreationDate, GetApiKeyCreationDate(user, identity));
                properties.Add(IsVerificationKeyUsed, hasVerifyScope);
                properties.Add(VerifyPackageKeyStatusCode, statusCode.ToString());
            });
        }

        public void TrackNewUserRegistrationEvent(User user, Credential credential)
        {
            TrackMetricForAccountActivity(Events.NewUserRegistration, user, credential);
        }

        public void TrackUserChangedMultiFactorAuthentication(User user, bool enabledMultiFactorAuth, string referrer = null)
        {
            TrackMetricForAccountActivity(enabledMultiFactorAuth ? Events.UserMultiFactorAuthenticationEnabled : Events.UserMultiFactorAuthenticationDisabled,
                user,
                credential: null,
                addProperties: addProperties => {
                    addProperties.Add("Referrer", referrer);
                });
        }

        public void TrackNewCredentialCreated(User user, Credential credential)
        {
            TrackMetricForAccountActivity(Events.CredentialAdded, user, credential);
        }

        public void TrackUserLogin(User user, Credential credential, bool wasMultiFactorAuthenticated)
        {
            TrackMetricForAccountActivity(Events.CredentialUsed, user, credential, addProperties => {
                addProperties.Add(WasMultiFactorAuthenticated, wasMultiFactorAuthenticated.ToString());
            });
        }

        public void TrackUserPackageDeleteExecuted(int packageKey, string packageId, string packageVersion, ReportPackageReason reason, bool success)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            TrackMetric(Events.UserPackageDeleteExecuted, 1, properties =>
            {
                properties.Add(PackageKey, packageKey.ToString());
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                properties.Add(ReportPackageReason, reason.ToString());
                properties.Add(Success, success.ToString());
            });
        }

        public void TrackPackageUnlisted(Package package)
        {
            TrackMetricForPackage(Events.PackageUnlisted, package);
        }

        public void TrackPackageListed(Package package)
        {
            TrackMetricForPackage(Events.PackageListed, package);
        }

        public void TrackPackagesUpdateListed(IReadOnlyList<Package> packages, bool listed)
        {
            TrackMetricForPackageVersions(
                Events.PackagesUpdateListed,
                packages,
                properties =>
                {
                    properties.Add(Listed, listed.ToString());
                });
        }

        public void TrackPackageDelete(Package package, bool isHardDelete)
        {
            TrackMetricForPackage(Events.PackageDelete, package, properties =>
            {
                properties.Add(IsHardDelete, isHardDelete.ToString());
            });
        }

        public void TrackPackageReupload(Package package)
        {
            TrackMetricForPackage(Events.PackageReupload, package);
        }

        public void TrackPackageReflow(Package package)
        {
            TrackMetricForPackage(Events.PackageReflow, package);
        }

        public void TrackPackageHardDeleteReflow(string packageId, string packageVersion)
        {
            TrackMetricForPackage(Events.PackageHardDeleteReflow, packageId, packageVersion);
        }

        public void TrackPackageRevalidate(Package package)
        {
            TrackMetricForPackage(Events.PackageRevalidate, package);
        }

        public void TrackPackageDeprecate(
            IReadOnlyList<Package> packages,
            PackageDeprecationStatus status,
            PackageRegistration alternateRegistration,
            Package alternatePackage,
            bool hasCustomMessage,
            bool hasChanges)
        {
            TrackMetricForPackageVersions(
                Events.PackageDeprecate,
                packages,
                properties =>
                {
                    properties.Add(DeprecationReason, ((int)status).ToString());
                    properties.Add(DeprecationAlternatePackageId, alternateRegistration?.Id ?? alternatePackage?.Id);
                    properties.Add(DeprecationAlternatePackageVersion, alternatePackage?.NormalizedVersion);
                    properties.Add(DeprecationCustomMessage, hasCustomMessage.ToString());
                    properties.Add(DeprecationHasChanges, hasChanges.ToString());
                });
        }

        public void TrackPackageMetadataComplianceError(string packageId, string packageVersion, IEnumerable<string> complianceFailures)
        {
            TrackMetricForPackage(
                Events.PackageMetadataComplianceError,
                packageId,
                packageVersion,
                properties =>
                {
                    properties.Add(ComplianceFailures, JsonConvert.SerializeObject(complianceFailures, _defaultJsonSerializerSettings));
                });
        }

        public void TrackPackageMetadataComplianceWarning(string packageId, string packageVersion, IEnumerable<string> complianceWarnings)
        {
            TrackMetricForPackage(
                Events.PackageMetadataComplianceWarning,
                packageId,
                packageVersion,
                properties =>
                {
                    properties.Add(ComplianceWarnings, JsonConvert.SerializeObject(complianceWarnings, _defaultJsonSerializerSettings));
                });
        }

        public void TrackPackageOwnershipAutomaticallyAdded(string packageId, string packageVersion)
        {
            TrackMetricForPackage(
                Events.PackageOwnershipAutomaticallyAdded,
                packageId,
                packageVersion);
        }

        public void TrackCertificateAdded(string thumbprint)
        {
            TrackMetricForCertificateActivity(Events.CertificateAdded, thumbprint);
        }

        public void TrackCertificateActivated(string thumbprint)
        {
            TrackMetricForCertificateActivity(Events.CertificateActivated, thumbprint);
        }

        public void TrackCertificateDeactivated(string thumbprint)
        {
            TrackMetricForCertificateActivity(Events.CertificateDeactivated, thumbprint);
        }

        public void TrackRequiredSignerSet(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(ServicesStrings.ArgumentCannotBeNullOrEmpty, nameof(packageId));
            }

            TrackMetric(Events.PackageRegistrationRequiredSignerSet, 1, properties =>
            {
                properties.Add(PackageId, packageId);
            });
        }

        public void TrackException(Exception exception, Action<Dictionary<string, string>> addProperties)
        {
            var telemetryProperties = new Dictionary<string, string>();

            addProperties(telemetryProperties);

            _telemetryClient.TrackException(exception, telemetryProperties, metrics: null);
        }

        public void TrackSymbolPackagePushEvent(string packageId, string packageVersion)
        {
            TrackMetricForSymbolPackage(Events.SymbolPackagePush, packageId, packageVersion);
        }
        public void TrackSymbolPackageDeleteEvent(string packageId, string packageVersion)
        {
            TrackMetricForSymbolPackage(Events.SymbolPackageDelete, packageId, packageVersion);
        }

        public void TrackSymbolPackagePushFailureEvent(string packageId, string packageVersion)
        {
            TrackMetricForSymbolPackage(Events.SymbolPackagePushFailure, packageId, packageVersion);
        }

        public void TrackSymbolPackageFailedGalleryValidationEvent(string packageId, string packageVersion)
        {
            TrackMetricForSymbolPackage(Events.SymbolPackageGalleryValidation, packageId, packageVersion);
        }

        public void TrackSymbolPackageRevalidate(string packageId, string packageVersion)
        {
            TrackMetricForSymbolPackage(Events.SymbolPackageRevalidate, packageId, packageVersion);
        }

        private void TrackMetricForAccountActivity(string eventName,
            User user,
            Credential credential,
            Action<Dictionary<string, string>> addProperties = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            TrackMetric(eventName, 1, properties =>
            {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(AccountCreationDate, GetAccountCreationDate(user));
                properties.Add(RegistrationMethod, GetRegistrationMethod(credential));
                addProperties?.Invoke(properties);
            });
        }

        private void TrackMetricForCertificateActivity(string eventName, string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new ArgumentException(ServicesStrings.ArgumentCannotBeNullOrEmpty, nameof(thumbprint));
            }

            TrackMetric(eventName, 1, properties =>
            {
                properties.Add(Sha256Thumbprint, thumbprint);
            });
        }
        private static string GetClientVersion()
        {
            return HttpContext.Current?.Request?.Headers[ServicesConstants.ClientVersionHeaderName];
        }

        private static string GetProtocolVersion()
        {
            return HttpContext.Current?.Request?.Headers[ServicesConstants.NuGetProtocolHeaderName];
        }

        private static string GetClientInformation()
        {
            if (HttpContext.Current != null)
            {
                HttpContextBase contextBase = new HttpContextWrapper(HttpContext.Current);
                return contextBase.GetClientInformation();
            }

            return null;
        }

        private static string GetAccountCreationDate(User user)
        {
            return user.CreatedUtc?.ToString("O") ?? "N/A";
        }

        private static string GetRegistrationMethod(Credential cred)
        {
            return cred?.Type ?? "";
        }

        private static string GetApiKeyCreationDate(User user, IIdentity identity)
        {
            var apiKey = user.GetCurrentApiKeyCredential(identity);
            return apiKey?.Created.ToString("O") ?? "N/A";
        }

        private void TrackMetricForSymbolPackage(
            string metricName,
            string packageId,
            string packageVersion,
            Action<Dictionary<string, string>> addProperties = null)
        {
            TrackMetric(metricName, 1, properties =>
            {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(ClientInformation, GetClientInformation());
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                addProperties?.Invoke(properties);
            });
        }

        private void TrackMetricForPackage(
            string metricName,
            string packageId,
            string packageVersion,
            User user,
            IIdentity identity,
            Action<Dictionary<string, string>> addProperties = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            TrackMetric(metricName, 1, properties =>
            {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(ClientInformation, GetClientInformation());
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                properties.Add(AccountCreationDate, GetAccountCreationDate(user));
                properties.Add(AuthenticationMethod, identity.GetAuthenticationType());
                properties.Add(KeyCreationDate, GetApiKeyCreationDate(user, identity));
                properties.Add(IsScoped, identity.IsScopedAuthentication().ToString());
                addProperties?.Invoke(properties);
            });
        }

        private void TrackMetricForPackage(
            string metricName,
            Package package,
            Action<Dictionary<string, string>> addProperties = null)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            TrackMetricForPackage(
                metricName,
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                addProperties);
        }

        private void TrackMetricForPackage(
            string metricName,
            string packageId,
            string packageVersion,
            Action<Dictionary<string, string>> addProperties = null)
        {
            TrackMetric(metricName, 1, properties =>
            {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(ClientInformation, GetClientInformation());
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                addProperties?.Invoke(properties);
            });
        }

        private void TrackMetricForPackageVersions(
            string metricName,
            IReadOnlyList<Package> packages,
            Action<Dictionary<string, string>> addProperties = null)
        {
            if (packages == null || !packages.Any() || packages.Select(p => p.PackageRegistrationKey).Distinct().Count() > 1)
            {
                throw new ArgumentException(nameof(packages));
            }

            TrackMetricForPackageVersions(
                metricName,
                packages.First().PackageRegistration.Id,
                packages.Select(p => p.NormalizedVersion).ToList(),
                addProperties);
        }

        private void TrackMetricForPackageVersions(
            string metricName,
            string packageId,
            IReadOnlyList<string> packageVersions,
            Action<Dictionary<string, string>> addProperties = null)
        {
            TrackMetric(metricName, packageVersions.Count(), properties =>
            {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(ClientInformation, GetClientInformation());
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, BuildArrayProperty(packageVersions));
                addProperties?.Invoke(properties);
            });
        }

        public void TrackUserPackageDeleteChecked(UserPackageDeleteEvent details, UserPackageDeleteOutcome outcome)
        {
            if (details == null)
            {
                throw new ArgumentNullException(nameof(details));
            }

            var hours = details.SinceCreated.TotalHours;

            TrackMetric(Events.UserPackageDeleteCheckedAfterHours, hours, properties =>
            {
                properties.Add(Outcome, outcome.ToString());
                properties.Add(PackageKey, details.PackageKey.ToString());
                properties.Add(PackageId, details.PackageId);
                properties.Add(PackageVersion, details.PackageVersion);
                properties.Add(IdDatabaseDownloads, details.IdDatabaseDownloads.ToString());
                properties.Add(IdReportDownloads, details.IdReportDownloads.ToString());
                properties.Add(VersionDatabaseDownloads, details.VersionDatabaseDownloads.ToString());
                properties.Add(VersionReportDownloads, details.VersionReportDownloads.ToString());
                properties.Add(ReportPackageReason, details.ReportPackageReason?.ToString());
                properties.Add(PackageDeleteDecision, details.PackageDeleteDecision?.ToString());
            });
        }

        private void TrackMetricForOrganization(
            string metricName,
            User user,
            Action<Dictionary<string, string>> addProperties = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            TrackMetric(metricName, 1, properties =>
            {
                properties.Add(OrganizationAccountKey, user.Key.ToString());
                properties.Add(AccountCreationDate, GetAccountCreationDate(user));
                properties.Add(OrganizationIsRestrictedToOrganizationTenantPolicy, user.IsRestrictedToOrganizationTenantPolicy().ToString());
                addProperties?.Invoke(properties);
            });
        }

        public void TrackOrganizationTransformInitiated(User user)
        {
            TrackMetricForOrganization(Events.OrganizationTransformInitiated, user);
        }

        public void TrackOrganizationTransformCompleted(User user)
        {
            TrackMetricForOrganization(Events.OrganizationTransformCompleted, user);
        }

        public void TrackOrganizationTransformDeclined(User user)
        {
            TrackMetricForOrganization(Events.OrganizationTransformDeclined, user);
        }

        public void TrackOrganizationTransformCancelled(User user)
        {
            TrackMetricForOrganization(Events.OrganizationTransformCancelled, user);
        }

        public void TrackOrganizationAdded(Organization organization)
        {
            TrackMetricForOrganization(Events.OrganizationAdded, organization);
        }

        public void TrackAccountDeletionCompleted(User deletedUser, User deletedBy, bool success)
        {
            if (deletedUser == null)
            {
                throw new ArgumentNullException(nameof(deletedUser));
            }
            if (deletedBy == null)
            {
                throw new ArgumentNullException(nameof(deletedBy));
            }

            TrackMetric(Events.AccountDeleteCompleted, 1, properties =>
            {
                properties.Add(AccountDeletedByRole, BuildArrayProperty(deletedBy.Roles?.Select(role => role.Name) ?? Array.Empty<string>()));
                properties.Add(AccountIsSelfDeleted, $"{deletedUser.Key == deletedBy.Key}");
                properties.Add(AccountDeletedIsOrganization, $"{deletedUser is Organization}");
                properties.Add(AccountDeleteSucceeded, $"{success}");
            });
        }

        public void TrackRequestForAccountDeletion(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            TrackMetric(Events.AccountDeleteRequested, 1, properties =>
            {
                properties.Add(CreatedDateForAccountToBeDeleted, $"{user.CreatedUtc}");
            });
        }

        public void TrackSendEmail(string smtpUri, DateTimeOffset startTime, TimeSpan duration, bool success, int attemptNumber)
        {
            var properties = new Dictionary<string, string>
            {
                { "attempt", attemptNumber.ToString() }
            };
            _telemetryClient.TrackDependency("SMTP", smtpUri, "SendMessage", null, startTime, duration, null, success, properties);
        }

        public void TrackMetricForTyposquattingCheckResultAndTotalTime(
            string packageId,
            TimeSpan totalTime,
            bool wasUploadBlocked,
            List<string> collisionPackageIds,
            int checkListLength,
            TimeSpan checkListCacheExpireTime)
        {
            TrackMetric(Events.TyposquattingCheckResultAndTotalTimeInMs, totalTime.TotalMilliseconds, properties =>
            {
                properties.Add(PackageId, packageId);
                properties.Add(WasUploadBlocked, wasUploadBlocked.ToString());
                properties.Add(CollisionPackageIds, BuildArrayProperty(collisionPackageIds.Take(TyposquattingCollisionIdsMaxPropertyValue)));
                properties.Add(CollisionPackageIdsCount, collisionPackageIds.Count.ToString());
                properties.Add(CheckListLength, checkListLength.ToString());
                properties.Add(HasExtraCollisionPackageIds, (collisionPackageIds.Count > TyposquattingCollisionIdsMaxPropertyValue).ToString());
                properties.Add(CheckListCacheExpireTimeInHours, checkListCacheExpireTime.ToString());
            });
        }

        public void TrackMetricForTyposquattingChecklistRetrievalTime(string packageId, TimeSpan checklistRetrievalTime)
        {
            TrackMetric(Events.TyposquattingChecklistRetrievalTimeInMs, checklistRetrievalTime.TotalMilliseconds, properties =>
            {
                properties.Add(PackageId, packageId);
            });
        }

        public void TrackMetricForTyposquattingAlgorithmProcessingTime(string packageId, TimeSpan algorithmProcessingTime)
        {
            TrackMetric(Events.TyposquattingAlgorithmProcessingTimeInMs, algorithmProcessingTime.TotalMilliseconds, properties =>
            {
                properties.Add(PackageId, packageId);
            });
        }

        public void TrackMetricForTyposquattingOwnersCheckTime(string packageId, TimeSpan ownersCheckTime)
        {
            TrackMetric(Events.TyposquattingOwnersCheckTimeInMs, ownersCheckTime.TotalMilliseconds, properties =>
            {
                properties.Add(PackageId, packageId);
            });
        }

        public void TrackInvalidLicenseMetadata(string licenseValue)
            => TrackMetric(Events.InvalidLicenseMetadata, 1, p => p.Add(LicenseExpression, licenseValue));

        public void TrackNonFsfOsiLicenseUse(string licenseExpression)
            => TrackMetric(Events.NonFsfOsiLicenseUsed, 1, p => p.Add(LicenseExpression, licenseExpression));

        public void TrackLicenseFileRejected()
            => TrackMetric(Events.LicenseFileRejected, 1, p => { });

        public void TrackLicenseValidationFailure()
            => TrackMetric(Events.LicenseValidationFailed, 1, p => { });

        public void TrackFeatureFlagStaleness(TimeSpan staleness)
            => TrackMetric(Events.FeatureFlagStalenessSeconds, staleness.TotalSeconds, p => { });

        public void TrackMetricForSearchExecutionDuration(string url, TimeSpan duration, bool executionSuccessStatus)
        {
            TrackMetric(Events.SearchExecutionDuration, duration.TotalMilliseconds, properties =>
            {
                properties.Add(SearchUrl, url);
                properties.Add(SearchSuccessExecutionStatus, executionSuccessStatus.ToString());
            });
        }

        public void TrackMetricForSearchCircuitBreakerOnBreak(string searchName, Exception exception, HttpResponseMessage responseMessage, string correlationId, string uri)
        {
            TrackMetric(Events.SearchCircuitBreakerOnBreak, 1, properties =>
            {
                properties.Add(SearchName, searchName);
                properties.Add(SearchException, exception?.ToString() ?? string.Empty);
                properties.Add(SearchHttpResponseCode, responseMessage?.StatusCode.ToString() ?? string.Empty);
                properties.Add(SearchPollyCorrelationId, correlationId);
                properties.Add(SearchUrl, uri);
            });
        }

        public void TrackMetricForSearchCircuitBreakerOnReset(string searchName, string correlationId, string uri)
        {
            TrackMetric(Events.SearchCircuitBreakerOnReset, 1, properties =>
            {
                properties.Add(SearchName, searchName);
                properties.Add(SearchPollyCorrelationId, correlationId);
                properties.Add(SearchUrl, uri);
            });
        }

        public void TrackMetricForSearchOnRetry(string searchName, Exception exception, string correlationId, string uri, string circuitBreakerStatus)
        {
            TrackMetric(Events.SearchOnRetry, 1, properties =>
            {
                properties.Add(SearchName, searchName);
                properties.Add(SearchException, exception?.ToString() ?? string.Empty);
                properties.Add(SearchPollyCorrelationId, correlationId);
                properties.Add(SearchUrl, uri);
                properties.Add(SearchCircuitBreakerStatus, circuitBreakerStatus);
            });
        }

        public void TrackMetricForSearchOnTimeout(string searchName, string correlationId, string uri, string circuitBreakerStatus)
        {
            TrackMetric(Events.SearchOnTimeout, 1, properties =>
            {
                properties.Add(SearchName, searchName);
                properties.Add(SearchPollyCorrelationId, correlationId);
                properties.Add(SearchUrl, uri);
                properties.Add(SearchCircuitBreakerStatus, circuitBreakerStatus);
            });
        }

        public void TrackSearchSideBySideFeedback(
            string searchTerm,
            int oldHits,
            int newHits,
            string betterSide,
            string mostRelevantPackage,
            string expectedPackages,
            bool hasComments,
            bool hasEmailAddress)
        {
            TrackMetric(Events.SearchSideBySideFeedback, 1, properties =>
            {
                properties.Add(SearchTerm, searchTerm);
                properties.Add(OldHits, oldHits.ToString());
                properties.Add(NewHits, newHits.ToString());
                properties.Add(BetterSide, betterSide);
                properties.Add(MostRelevantPackage, mostRelevantPackage);
                properties.Add(ExpectedPackages, expectedPackages);
                properties.Add(HasComments, hasComments.ToString());
                properties.Add(HasEmailAddress, hasEmailAddress.ToString());
            });
        }

        public void TrackSearchSideBySide(
            string searchTerm,
            bool oldSuccess,
            int oldHits,
            bool newSuccess,
            int newHits)
        {
            TrackMetric(Events.SearchSideBySide, 1, properties =>
            {
                properties.Add(SearchTerm, searchTerm);
                properties.Add(OldSuccess, oldSuccess.ToString());
                properties.Add(OldHits, oldHits.ToString());
                properties.Add(NewSuccess, newSuccess.ToString());
                properties.Add(NewHits, newHits.ToString());
            });
        }

        public void TrackABTestEnrollmentInitialized(
            int newSchemaVersion,
            int previewSearchBucket,
            int packageDependentBucket)
        {
            TrackMetric(Events.ABTestEnrollmentInitialized, 1, properties =>
            {
                properties.Add(NewSchemaVersion, newSchemaVersion.ToString());
                properties.Add(PreviewSearchBucket, previewSearchBucket.ToString());
                properties.Add(PackageDependentBucket, packageDependentBucket.ToString());
            });
        }

        public void TrackABTestEnrollmentUpgraded(
            int oldSchemaVersion,
            int newSchemaVersion,
            int previewSearchBucket,
            int packageDependentBucket)
        {
            TrackMetric(Events.ABTestEnrollmentUpgraded, 1, properties =>
            {
                properties.Add(OldSchemaVersion, oldSchemaVersion.ToString());
                properties.Add(NewSchemaVersion, newSchemaVersion.ToString());
                properties.Add(PreviewSearchBucket, previewSearchBucket.ToString());
                properties.Add(PackageDependentBucket, packageDependentBucket.ToString());
            });
        }

        public void TrackABTestEvaluated(
            string name,
            bool isActive,
            bool isAuthenticated,
            int testBucket,
            int testPercentage)
        {
            TrackMetric(Events.ABTestEvaluated, 1, properties =>
            {
                properties.Add(TestName, name);
                properties.Add(IsActive, isActive.ToString());
                properties.Add(IsAuthenticated, isAuthenticated.ToString());
                properties.Add(TestBucket, testBucket.ToString());
                properties.Add(TestPercentage, testPercentage.ToString());
            });
        }

        public void TrackPackagePushDisconnectEvent()
        {
            TrackMetric(Events.PackagePushDisconnect, 1, _ => { });
        }

        public void TrackSymbolPackagePushDisconnectEvent()
        {
            TrackMetric(Events.SymbolPackagePushDisconnect, 1, _ => { });
        }

        public void TrackInstanceUptime(TimeSpan uptime)
        {
            TrackMetric(Events.InstanceUptime, uptime.TotalDays, _ => { });
        }

        public void TrackVulnerabilitiesCacheRefreshDuration(TimeSpan duration)
        {
            TrackMetric(Events.VulnerabilitiesCacheRefreshDurationMs, duration.TotalMilliseconds, properties => { });
        }

        public void TrackApiRequest(string endpoint)
        {
            _telemetryClient.TrackAggregatedMetric(Events.ApiRequest, 1, Endpoint, endpoint);
        }

        public IDisposable TrackSyncSqlConnectionCreationDuration()
            => TrackSqlConnectionCreationDuration(Sync);

        public IDisposable TrackAsyncSqlConnectionCreationDuration()
            => TrackSqlConnectionCreationDuration(Async);

        private IDisposable TrackSqlConnectionCreationDuration(string kind)
        {
            return new DurationTracker(duration => 
                _telemetryClient.TrackAggregatedMetric(Events.CreateSqlConnectionDurationMs, duration.TotalMilliseconds, Kind, kind));
        }

        /// <summary>
        /// We use <see cref="ITelemetryClient.TrackMetric(string, double, IDictionary{string, string})"/> instead of
        /// <see cref="ITelemetryClient.TrackEvent(string, IDictionary{string, string}, IDictionary{string, double})"/>
        /// because events don't flow properly into our internal metrics and monitoring solution.
        /// </summary>
        protected virtual void TrackMetric(string metricName, double value, Action<Dictionary<string, string>> addProperties)
        {
            var telemetryProperties = new Dictionary<string, string>();

            addProperties(telemetryProperties);

            _telemetryClient.TrackMetric(metricName, value, telemetryProperties);
        }

        private string BuildArrayProperty(IEnumerable<string> list)
        {
            return JsonConvert.SerializeObject(list);
        }
    }
}
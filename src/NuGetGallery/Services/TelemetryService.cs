﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Web;
using NuGetGallery.Authentication;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    public class TelemetryService : ITelemetryService
    {
        internal class Events
        {
            public const string ODataQueryFilter = "ODataQueryFilter";
            public const string PackagePush = "PackagePush";
            public const string CreatePackageVerificationKey = "CreatePackageVerificationKey";
            public const string VerifyPackageKey = "VerifyPackageKey";
            public const string PackageReadMeChanged = "PackageReadMeChanged";
            public const string PackagePushNamespaceConflict = "PackagePushNamespaceConflict";
            public const string NewUserRegistration = "NewUserRegistration";
            public const string CredentialAdded = "CredentialAdded";
            public const string UserPackageDeleteCheckedAfterHours = "UserPackageDeleteCheckedAfterHours";
            public const string UserPackageDeleteExecuted = "UserPackageDeleteExecuted";
            public const string UserMultiFactorAuthenticationEnabled = "UserMultiFactorAuthenticationEnabled";
            public const string UserMultiFactorAuthenticationDisabled = "UserMultiFactorAuthenticationDisabled";
            public const string PackageReflow = "PackageReflow";
            public const string PackageUnlisted = "PackageUnlisted";
            public const string PackageListed = "PackageListed";
            public const string PackageDelete = "PackageDelete";
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
        }

        private IDiagnosticsSource _diagnosticsSource;
        private ITelemetryClient _telemetryClient;

        // ODataQueryFilter properties
        public const string CallContext = "CallContext";
        public const string IsEnabled = "IsEnabled";
        public const string IsAllowed = "IsAllowed";
        public const string QueryPattern = "QueryPattern";

        // Package push properties
        public const string AuthenticationMethod = "AuthenticationMethod";
        public const string ClientVersion = "ClientVersion";
        public const string ProtocolVersion = "ProtocolVersion";
        public const string ClientInformation = "ClientInformation";
        public const string IsAuthenticated = "IsAuthenticated";
        public const string IsScoped = "IsScoped";
        public const string KeyCreationDate = "KeyCreationDate";
        public const string PackageId = "PackageId";
        public const string PackageVersion = "PackageVersion";

        // User properties
        public const string RegistrationMethod = "RegistrationMethod";
        public const string AccountCreationDate = "AccountCreationDate";

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

        public TelemetryService(IDiagnosticsService diagnosticsService, ITelemetryClient telemetryClient = null)
        {
            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsService));
            }

            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            _diagnosticsSource = diagnosticsService.GetSource("TelemetryService");
        }

        // Used by ODataQueryVerifier. Should consider refactoring to make this non-static.
        internal TelemetryService() : this(new DiagnosticsService(), TelemetryClientWrapper.Instance)
        {
        }

        public void TraceException(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            _diagnosticsSource.Warning(exception.ToString());
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

            TrackMetric(Events.PackageReadMeChanged, 1, properties => {
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

        public void TrackPackagePushNamespaceConflictEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            TrackMetricForPackage(Events.PackagePushNamespaceConflict, packageId, packageVersion, user, identity);
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

        public void TrackUserChangedMultiFactorAuthentication(User user, bool enabledMultiFactorAuth)
        {
            TrackMetricForAccountActivity(enabledMultiFactorAuth ? Events.UserMultiFactorAuthenticationEnabled : Events.UserMultiFactorAuthenticationDisabled,
                user,
                credential: null);
        }

        public void TrackNewCredentialCreated(User user, Credential credential)
        {
            TrackMetricForAccountActivity(Events.CredentialAdded, user, credential);
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

            TrackMetric(Events.UserPackageDeleteExecuted, 1, properties => {
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

        public void TrackPackageDelete(Package package, bool isHardDelete)
        {
            TrackMetricForPackage(Events.PackageDelete, package, properties =>
            {
                properties.Add(IsHardDelete, isHardDelete.ToString());
            });
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
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(packageId));
            }

            TrackMetric(Events.PackageRegistrationRequiredSignerSet, 1, properties => {
                properties.Add(PackageId, packageId);
            });
        }

        public void TrackException(Exception exception, Action<Dictionary<string, string>> addProperties)
        {
            var telemetryProperties = new Dictionary<string, string>();

            addProperties(telemetryProperties);

            _telemetryClient.TrackException(exception, telemetryProperties, metrics: null);
        }

        private void TrackMetricForAccountActivity(string eventName, User user, Credential credential)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            TrackMetric(eventName, 1, properties => {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(AccountCreationDate, GetAccountCreationDate(user));
                properties.Add(RegistrationMethod, GetRegistrationMethod(credential));
            });
        }

        private void TrackMetricForCertificateActivity(string eventName, string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(thumbprint));
            }

            TrackMetric(eventName, 1, properties => {
                properties.Add(Sha256Thumbprint, thumbprint);
            });
        }

        private static string GetClientVersion()
        {
            return HttpContext.Current?.Request?.Headers[Constants.ClientVersionHeaderName];
        }

        private static string GetProtocolVersion()
        {
            return HttpContext.Current?.Request?.Headers[Constants.NuGetProtocolHeaderName];
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

        private void TrackMetricForPackage(
            string metricName,
            Package package,
            User user,
            IIdentity identity,
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
                user,
                identity,
                addProperties);
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

            TrackMetric(metricName, 1, properties => {
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
            TrackMetric(metricName, 1, properties => {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(ClientInformation, GetClientInformation());
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
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

            TrackMetric(Events.UserPackageDeleteCheckedAfterHours, hours, properties => {
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
    }
}
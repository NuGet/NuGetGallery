// Copyright (c) .NET Foundation. All rights reserved.
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
            TrackEvent(Events.ODataQueryFilter, properties =>
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

            TrackEvent(Events.PackageReadMeChanged, properties => {
                properties.Add(PackageId, package.PackageRegistration.Id);
                properties.Add(PackageVersion, package.Version);
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

            TrackPackageForEvent(Events.PackagePush, package.PackageRegistration.Id, package.Version, user, identity);
        }

        public void TrackPackagePushNamespaceConflictEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            TrackPackageForEvent(Events.PackagePushNamespaceConflict, packageId, packageVersion, user, identity);
        }

        public void TrackCreatePackageVerificationKeyEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            TrackPackageForEvent(Events.CreatePackageVerificationKey, packageId, packageVersion, user, identity);
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

            TrackEvent(Events.VerifyPackageKey, properties =>
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
            TrackAccountActivityForEvent(Events.NewUserRegistration, user, credential);
        }

        public void TrackNewCredentialCreated(User user, Credential credential)
        {
            TrackAccountActivityForEvent(Events.CredentialAdded, user, credential);
        }

        private void TrackAccountActivityForEvent(string eventName, User user, Credential credential)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            TrackEvent(eventName, properties => {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(AccountCreationDate, GetAccountCreationDate(user));
                properties.Add(RegistrationMethod, GetRegistrationMethod(credential));
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
            return cred.Type;
        }

        private static string GetApiKeyCreationDate(User user, IIdentity identity)
        {
            var apiKey = user.GetCurrentApiKeyCredential(identity);
            return apiKey?.Created.ToString("O") ?? "N/A";
        }

        private void TrackPackageForEvent(string eventValue, string packageId, string packageVersion, User user, IIdentity identity)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            TrackEvent(eventValue, properties => {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(ProtocolVersion, GetProtocolVersion());
                properties.Add(ClientInformation, GetClientInformation());
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                properties.Add(AccountCreationDate, GetAccountCreationDate(user));
                properties.Add(AuthenticationMethod, identity.GetAuthenticationType());
                properties.Add(KeyCreationDate, GetApiKeyCreationDate(user, identity));
                properties.Add(IsScoped, identity.IsScopedAuthentication().ToString());
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

        protected virtual void TrackEvent(string eventName, Action<Dictionary<string, string>> addProperties)
        {
            var telemetryProperties = new Dictionary<string, string>();

            addProperties(telemetryProperties);

            _telemetryClient.TrackEvent(eventName, telemetryProperties, metrics: null);
        }

        protected virtual void TrackMetric(string metricName, double value, Action<Dictionary<string, string>> addProperties)
        {
            var telemetryProperties = new Dictionary<string, string>();

            addProperties(telemetryProperties);

            _telemetryClient.TrackMetric(metricName, value, telemetryProperties);
        }

        public void TrackException(Exception exception, Action<Dictionary<string, string>> addProperties)
        {
            var telemetryProperties = new Dictionary<string, string>();

            addProperties(telemetryProperties);

            _telemetryClient.TrackException(exception, telemetryProperties, metrics: null);
        }
    }
}
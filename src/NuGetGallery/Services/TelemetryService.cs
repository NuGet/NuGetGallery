// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Web;

namespace NuGetGallery
{
    public class TelemetryService : ITelemetryService
    {
        // ODataQueryFilter
        public const string ODataQueryFilter = "ODataQueryFilter";
        public const string CallContext = "CallContext";
        public const string IsEnabled = "IsEnabled";
        public const string IsAllowed = "IsAllowed";
        public const string QueryPattern = "QueryPattern";

        // Package events
        public const string PackagePushEvent = "PackagePush";
        public const string SymbolsPushEvent = "SymbolsPush";
        public const string SymbolsPushCallbackEvent = "SymbolsPushCallback";

        // Package event properties
        public const string AuthenticationMethod = "AuthenticationMethod";
        public const string AccountCreationDate = "AccountCreationDate";
        public const string ClientVersion = "ClientVersion";
        public const string IsScoped = "IsScoped";
        public const string HasVerifyScope = "HasVerifyScope";
        public const string PackageId = "PackageId";
        public const string PackageVersion = "PackageVersion";
        public const string SymbolsStatusCode = "SymbolsStatusCode";

        public void TrackODataQueryFilterEvent(string callContext, bool isEnabled, bool isAllowed, string queryPattern)
        {
            var telemetryProperties = new Dictionary<string, string>();

            telemetryProperties.Add(CallContext, callContext);
            telemetryProperties.Add(IsEnabled, $"{isEnabled}");

            telemetryProperties.Add(IsAllowed, $"{isAllowed}");
            telemetryProperties.Add(QueryPattern, queryPattern);

            Telemetry.TrackEvent(ODataQueryFilter, telemetryProperties, metrics: null);
        }

        public void TrackPackagePushEvent(Package package, User user, IIdentity identity)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            TrackPackageEvent(PackagePushEvent, package.PackageRegistration.Id, package.NormalizedVersion, user, identity);
        }

        public void TrackSymbolsPushEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            TrackPackageEvent(SymbolsPushEvent, packageId, packageVersion, user, identity);
        }

        public void TrackSymbolsPushCallbackEvent(string packageId, string packageVersion, User user, IIdentity identity, int statusCode)
        {
            TrackPackageEvent(SymbolsPushCallbackEvent, packageId, packageVersion, user, identity,
                properties => properties.Add(SymbolsStatusCode, statusCode.ToString()));
        }

        private void TrackPackageEvent(string eventName, string packageId, string packageVersion, User user, IIdentity identity,
            Action<Dictionary<string, string>> addCustomProperties = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            var authenticationMethod = identity.GetAuthenticationType();
            var isScoped = identity.IsScopedAuthentication();
            var hasVerifyScope = isScoped && identity.HasVerifyScope();
            var clientVersion = HttpContext.Current?.Request?.Headers[Constants.ClientVersionHeaderName];

            var telemetryProperties = new Dictionary<string, string>();
            telemetryProperties.Add(ClientVersion, clientVersion);
            telemetryProperties.Add(PackageId, packageId);
            telemetryProperties.Add(PackageVersion, packageVersion);
            telemetryProperties.Add(AuthenticationMethod, authenticationMethod);
            telemetryProperties.Add(AccountCreationDate, user.CreatedUtc != null ? user.CreatedUtc.Value.ToString("d") : "N/A");
            telemetryProperties.Add(IsScoped, isScoped.ToString());
            telemetryProperties.Add(HasVerifyScope, hasVerifyScope.ToString());

            addCustomProperties?.Invoke(telemetryProperties);

            Telemetry.TrackEvent(eventName, telemetryProperties, metrics: null);
        }
    }
}
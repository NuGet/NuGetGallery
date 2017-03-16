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
        // Event types
        public const string ODataQueryFilterEvent = "ODataQueryFilter";
        public const string PackagePushEvent = "PackagePush";
        public const string CreatePackageVerificationKeyEvent = "CreatePackageVerificationKeyEvent";
        public const string VerifyPackageKeyEvent = "VerifyPackageKeyEvent";

        // ODataQueryFilter properties
        public const string CallContext = "CallContext";
        public const string IsEnabled = "IsEnabled";
        public const string IsAllowed = "IsAllowed";
        public const string QueryPattern = "QueryPattern";

        // Package push properties
        public const string AuthenticationMethod = "AuthenticationMethod";
        public const string AccountCreationDate = "AccountCreationDate";
        public const string ClientVersion = "ClientVersion";
        public const string IsScoped = "IsScoped";
        public const string PackageId = "PackageId";
        public const string PackageVersion = "PackageVersion";

        // Verify package properties
        public const string HasVerifyScope = "HasVerifyScope";
        public const string VerifyPackageKeyStatusCode = "VerifyPackageKeyStatusCode";

        public void TrackODataQueryFilterEvent(string callContext, bool isEnabled, bool isAllowed, string queryPattern)
        {
            TrackEvent(ODataQueryFilterEvent, properties =>
            {
                properties.Add(CallContext, callContext);
                properties.Add(IsEnabled, $"{isEnabled}");

                properties.Add(IsAllowed, $"{isAllowed}");
                properties.Add(QueryPattern, queryPattern);
            });
        }

        public void TrackPackagePushEvent(Package package, User user, IIdentity identity)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            TrackEvent(PackagePushEvent, properties => {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(PackageId, package.PackageRegistration.Id);
                properties.Add(PackageVersion, package.Version);
                properties.Add(AuthenticationMethod, identity.GetAuthenticationType());
                properties.Add(AccountCreationDate, GetAccountCreationDate(user));
                properties.Add(IsScoped, identity.IsScopedAuthentication().ToString());
            });
        }

        public void TrackCreatePackageVerificationKeyEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            TrackEvent(CreatePackageVerificationKeyEvent, properties => {
                properties.Add(ClientVersion, GetClientVersion());
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                properties.Add(AccountCreationDate, GetAccountCreationDate(user));
                properties.Add(IsScoped, identity.IsScopedAuthentication().ToString());
            });
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

            TrackEvent(VerifyPackageKeyEvent, properties =>
            {
                properties.Add(PackageId, packageId);
                properties.Add(PackageVersion, packageVersion);
                properties.Add(HasVerifyScope, identity.HasVerifyScope().ToString());
                properties.Add(VerifyPackageKeyStatusCode, statusCode.ToString());
            });
        }

        private static string GetClientVersion()
        {
            return HttpContext.Current?.Request?.Headers[Constants.ClientVersionHeaderName];
        }

        private static string GetAccountCreationDate(User user)
        {
            return user.CreatedUtc != null ? user.CreatedUtc.Value.ToString("d") : "N/A";
        }

        private static void TrackEvent(string eventName, Action<Dictionary<string, string>> addProperties)
        {
            var telemetryProperties = new Dictionary<string, string>();

            addProperties(telemetryProperties);

            Telemetry.TrackEvent(eventName, telemetryProperties, metrics: null);
        }
    }
}
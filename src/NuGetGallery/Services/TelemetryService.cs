// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Principal;

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

        // Package push
        public const string PackagePush = "PackagePush";
        public const string AuthenticatinMethod = "AuthenticationMethod";
        public const string AccountCreationDate = "AccountCreationDate";
        public const string IsScoped = "IsScoped";

        public void TrackODataQueryFilterEvent(string callContext, bool isEnabled, bool isAllowed, string queryPattern)
        {
            var telemetryProperties = new Dictionary<string, string>();

            telemetryProperties.Add(CallContext, callContext);
            telemetryProperties.Add(IsEnabled, $"{isEnabled}");

            telemetryProperties.Add(IsAllowed, $"{isAllowed}");
            telemetryProperties.Add(QueryPattern, queryPattern);

            Telemetry.TrackEvent(ODataQueryFilter, telemetryProperties, metrics: null);
        }

        public void TrackPackagePushEvent(User user, IIdentity identity)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            string authenticationMethod = identity.GetAuthenticationType();
            bool isScoped = identity.IsScopedAuthentication();

            var telemetryProperties = new Dictionary<string, string>();
            telemetryProperties.Add(AuthenticatinMethod, authenticationMethod);
            telemetryProperties.Add(AccountCreationDate, user.CreatedUtc != null ? user.CreatedUtc.Value.ToString("d") : "N/A");
            telemetryProperties.Add(IsScoped, isScoped.ToString());

            Telemetry.TrackEvent(PackagePush, telemetryProperties, metrics: null);
        }
    }
}
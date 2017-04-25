// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGetGallery.Filters;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policy that requires a minimum client version in order to push packages.
    /// </summary>
    public class RequireMinClientVersionForPushPolicy : UserSecurityPolicyHandler
    {
        public class State
        {
            [JsonProperty("v")]
            [JsonConverter(typeof(VersionConverter))]
            public Version MinClientVersion { get; set; }
        }

        public RequireMinClientVersionForPushPolicy()
            : base(nameof(RequireMinClientVersionForPushPolicy), SecurityPolicyAction.PackagePush)
        {
        }

        /// <summary>
        /// In case of multiple, select the max of the minimum required client versions.
        /// </summary>
        private Version GetMaxOfMinClientVersions(UserSecurityPolicyContext context)
        {
            var policyStates = context.Policies.Select(p => JsonConvert.DeserializeObject<State>(p.Value));
            return policyStates.Max(s => s.MinClientVersion);
        }

        /// <summary>
        /// Get the current client version from the request.
        /// </summary>
        private Version GetClientVersion(UserSecurityPolicyContext context)
        {
            var clientVersionString = context.HttpContext.Request?.Headers[Constants.ClientVersionHeaderName];

            Version clientVersion;
            return Version.TryParse(clientVersionString, out clientVersion) ? clientVersion : null;
        }
        
        public override SecurityPolicyResult Evaluate(UserSecurityPolicyContext context)
        {
            var minClientVersion = GetMaxOfMinClientVersions(context);

            var clientVersion = GetClientVersion(context);
            if (clientVersion == null || clientVersion < minClientVersion)
            {
                return new SecurityPolicyResult(false, string.Format(CultureInfo.CurrentCulture,
                    Strings.SecurityPolicy_RequireMinClientVersionForPush, minClientVersion));
            }

            return SecurityPolicyResult.SuccessResult;
        }
    }
}
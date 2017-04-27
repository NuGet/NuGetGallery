// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Protocol;
using NuGet.Versioning;
using NuGetGallery.Filters;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policy that requires a minimum client version in order to push packages.
    /// </summary>
    public class RequireMinClientVersionForPushPolicy : UserSecurityPolicyHandler
    {
        public const string PolicyName = "RequireMinClientVersionForPushPolicy";

        public class State
        {
            [JsonProperty("v")]
            [JsonConverter(typeof(NuGetVersionConverter))]
            public NuGetVersion MinClientVersion { get; set; }
        }

        public RequireMinClientVersionForPushPolicy()
            : base(PolicyName, SecurityPolicyAction.PackagePush)
        {
        }

        /// <summary>
        /// In case of multiple, select the max of the minimum required client versions.
        /// </summary>
        private NuGetVersion GetMaxOfMinClientVersions(UserSecurityPolicyContext context)
        {
            var policyStates = context.Policies
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => JsonConvert.DeserializeObject<State>(p.Value));
            return policyStates.Max(s => s.MinClientVersion);
        }

        /// <summary>
        /// Get the current client version from the request.
        /// </summary>
        private NuGetVersion GetClientVersion(UserSecurityPolicyContext context)
        {
            var clientVersionString = context.HttpContext.Request?.Headers[Constants.ClientVersionHeaderName];

            NuGetVersion clientVersion;
            return NuGetVersion.TryParse(clientVersionString, out clientVersion) ? clientVersion : null;
        }
        
        public override SecurityPolicyResult Evaluate(UserSecurityPolicyContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var minClientVersion = GetMaxOfMinClientVersions(context);

            var clientVersion = GetClientVersion(context);
            if (clientVersion == null || clientVersion < minClientVersion)
            {
                return SecurityPolicyResult.CreateErrorResult(string.Format(CultureInfo.CurrentCulture,
                    Strings.SecurityPolicy_RequireMinClientVersionForPush, minClientVersion));
            }

            return SecurityPolicyResult.SuccessResult;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Protocol;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery.Security
{
    /// <summary>
    /// User security policy that requires a minimum protocol version in order to push packages.
    /// </summary>
    public class RequireMinProtocolVersionForPushPolicy : UserSecurityPolicyHandler
    {
        public const string PolicyName = nameof(RequireMinProtocolVersionForPushPolicy);

        public class State
        {
            [JsonProperty("v")]
            [JsonConverter(typeof(NuGetVersionConverter))]
            public NuGetVersion MinProtocolVersion { get; set; }
        }

        public RequireMinProtocolVersionForPushPolicy()
            : base(PolicyName, SecurityPolicyAction.PackagePush)
        {
        }

        /// <summary>
        /// Create a user security policy that requires a minimum protocol version.
        /// </summary>
        public static UserSecurityPolicy CreatePolicy(string subscription, NuGetVersion minProtocolVersion)
        {
            var value = JsonConvert.SerializeObject(new State()
            {
                MinProtocolVersion = minProtocolVersion
            });

            return new UserSecurityPolicy(PolicyName, subscription, value);
        }

        /// <summary>
        /// In case of multiple, select the max of the minimum required protocol versions.
        /// </summary>
        private NuGetVersion GetMaxOfMinProtocolVersions(UserSecurityPolicyEvaluationContext context)
        {
            var policyStates = context.Policies
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => JsonConvert.DeserializeObject<State>(p.Value));
            return policyStates.Max(s => s.MinProtocolVersion);
        }

        /// <summary>
        /// Get the current client version from the request. This header is DEPRECATED, and here for backwards compatibility!
        /// </summary>
        private NuGetVersion GetClientVersion(UserSecurityPolicyEvaluationContext context)
        {
            var clientVersionString = context.HttpContext.Request?.Headers[GalleryConstants.ClientVersionHeaderName];

            return NuGetVersion.TryParse(clientVersionString, out NuGetVersion clientVersion) ? clientVersion : null;
        }

        /// <summary>
        /// Get the current protocol version from the request.
        /// </summary>
        private NuGetVersion GetProtocolVersion(UserSecurityPolicyEvaluationContext context)
        {
            var protocolVersionString = context.HttpContext.Request?.Headers[GalleryConstants.NuGetProtocolHeaderName];

            return NuGetVersion.TryParse(protocolVersionString, out NuGetVersion protocolVersion) ? protocolVersion : null;
        }

        /// <summary>
        /// Evaluate if this security policy is met.
        /// </summary>
        public override Task<SecurityPolicyResult> EvaluateAsync(UserSecurityPolicyEvaluationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var minProtocolVersion = GetMaxOfMinProtocolVersions(context);

            // Do we have X-NuGet-Protocol-Version header?
            var protocolVersion = GetProtocolVersion(context);

            if (protocolVersion == null)
            {
                // Do we have X-NuGet-Client-Version header? This header is DEPRECATED, and here for backwards compatibility!
                protocolVersion = GetClientVersion(context);
            }

            if (protocolVersion == null || protocolVersion < minProtocolVersion)
            {
                return Task.FromResult(SecurityPolicyResult.CreateErrorResult(string.Format(CultureInfo.CurrentCulture,
                    Strings.SecurityPolicy_RequireMinProtocolVersionForPush, minProtocolVersion)));
            }

            return Task.FromResult(SecurityPolicyResult.SuccessResult);
        }
    }
}
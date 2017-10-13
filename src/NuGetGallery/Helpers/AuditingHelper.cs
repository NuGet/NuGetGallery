// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class AuditingHelper
    {
        public static Task<AuditActor> GetAspNetOnBehalfOfAsync()
        {
            // Use HttpContext to build an actor representing the user performing the action
            var context = HttpContext.Current;
            if (context == null)
            {
                return Task.FromResult<AuditActor>(null);
            }

            return GetAspNetOnBehalfOfAsync(new HttpContextWrapper(context));
        }

        public static Task<AuditActor> GetAspNetOnBehalfOfAsync(HttpContextBase context)
        {
            // Try to identify the client IP using various server variables
            var clientIpAddress = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (string.IsNullOrEmpty(clientIpAddress)) // Try REMOTE_ADDR server variable
            {
                clientIpAddress = context.Request.ServerVariables["REMOTE_ADDR"];
            }

            if (string.IsNullOrEmpty(clientIpAddress)) // Try UserHostAddress property
            {
                clientIpAddress = context.Request.UserHostAddress;
            }

            if (!string.IsNullOrEmpty(clientIpAddress) && clientIpAddress.IndexOf(".", StringComparison.Ordinal) > 0)
            {
                clientIpAddress = clientIpAddress.Substring(0, clientIpAddress.LastIndexOf(".", StringComparison.Ordinal)) + ".0";
            }

            string user = null;
            string authType = null;
            string credentialKey = null;

            if (context.User != null)
            {
                user = context.User.Identity.Name;
                authType = context.User.Identity.AuthenticationType;

                var claimsIdentity = context.User.Identity as ClaimsIdentity;
                credentialKey = claimsIdentity?.GetClaimOrDefault(NuGetClaims.CredentialKey);
            }

            return Task.FromResult(new AuditActor(
                null,
                clientIpAddress,
                user,
                authType,
                credentialKey,
                DateTime.UtcNow));
        }
    }
}
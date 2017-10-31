﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Principal;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    /// <summary>
    /// Extension methods for the NuGetGallery.User entity.
    /// </summary>
    public static class UserExtensions
    {
        /// <summary>
        /// Convert a User's email to a System.Net MailAddress.
        /// </summary>
        public static MailAddress ToMailAddress(this User user)
        {
            if (!user.Confirmed)
            {
                return new MailAddress(user.UnconfirmedEmailAddress, user.Username);
            }

            return new MailAddress(user.EmailAddress, user.Username);
        }

        /// <summary>
        /// Get the current API key credential, if available.
        /// </summary>
        public static Credential GetCurrentApiKeyCredential(this User user, IIdentity identity)
        {
            var claimsIdentity = identity as ClaimsIdentity;
            var apiKey = claimsIdentity.GetClaimOrDefault(NuGetClaims.ApiKey);

            return user.Credentials.FirstOrDefault(c => c.Value == apiKey);
        }

        /// <summary>
        /// Get the current API key package owner (user or organization) scope.
        /// </summary>
        /// <returns>Owner scope, or null for legacy API keys.</returns>
        public static string GetCurrentApiKeyOwnerScope(this User user, IIdentity identity)
        {
            // All scopes for a given API key should target the same owner.
            var credential = user.GetCurrentApiKeyCredential(identity);
            return credential.Scopes
                .Select(o => o.Owner)
                .Distinct()
                .SingleOrDefault();
        }
    }
}
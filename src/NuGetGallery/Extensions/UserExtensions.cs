// Copyright (c) .NET Foundation. All rights reserved.
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
        /// Determines if the User (account) belongs to an organization.
        /// </summary>
        /// <param name="account">User (account) instance.</param>
        /// <returns>True for organizations, false for users.</returns>
        public static bool IsOrganization(this User account)
        {
            return account.Organization != null;
        }
    }
}
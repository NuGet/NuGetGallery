// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Services.Security;

namespace NuGetGallery
{
    /// <summary>
    /// APIs that provide lightweight extensibility for the User entity.
    /// </summary>
    public static class UserExtensions
    {
        /// <summary>
        /// Get the user's <see cref="Credential"/> with a type of <see cref="CredentialTypes.Password"/>.
        /// </summary>
        public static Credential GetPasswordCredential(this User user)
        {
            return user.Credentials.SingleOrDefault(c => c.IsPassword());
        }

        /// <summary>
        /// Return true if the user has a credential of type <see cref="CredentialTypes.Password"/>.
        /// </summary>
        public static bool HasPasswordCredential(this User user)
        {
            return user.Credentials.Any(c => c.IsPassword());
        }

        /// <summary>
        /// Return true if the user has a credential of type <see cref="CredentialTypes.External"/>.
        /// </summary>
        public static bool HasExternalCredential(this User user)
        {
            return user.Credentials.Any(c => c.IsExternal());
        }

        /// <summary>
        /// Get the current API key credential, if available.
        /// </summary>
        public static Credential GetCurrentApiKeyCredential(this User user, IIdentity identity)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var claimsIdentity = identity as ClaimsIdentity;
            var apiKey = claimsIdentity.GetClaimOrDefault(NuGetClaims.ApiKey);

            return user.Credentials.FirstOrDefault(c => c.Value == apiKey);
        }

        /// <summary>
        /// Determine if user is direct or indirect (organization admin) package owner.
        /// </summary>
        /// <param name="user">User to query.</param>
        /// <param name="package">Package to query.</param>
        /// <returns>True if direct or indirect package owner.</returns>
        public static bool IsOwnerOrMemberOfOrganizationOwner(this User user, PackageRegistration package)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return package.Owners.Any(o => user.KeyIsSelfOrOrganization(o.Key));
        }

        public static bool MatchesUser(this User self, User user)
        {
            return self?.Key == user?.Key;
        }

        public static bool HasCredential(this User user, Credential credential)
        {
            return user.Credentials.Any(cred => cred.Matches(credential));
        }

        /// <summary>
        /// Determine if the current user matches the owner scope of the current credential.
        /// There is a match if the owner scope is self or an organization to which the user
        /// belongs.
        /// 
        /// Note there is no need to check organization role, which the action scope covers.
        /// </summary>
        /// <param name="user">User to query.</param>
        /// <param name="credential">Credential to query.</param>
        /// <returns>True if user matches the owner scope, false otherwise.</returns>
        public static bool MatchesOwnerScope(this User user, Credential credential)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            // Legacy V1 API key with no owner scope.
            if (!credential.Scopes.Any())
            {
                return true;
            }

            return credential.Scopes
                .Select(s => s.OwnerKey)
                .Distinct()
                .Any(ownerKey => !ownerKey.HasValue // Legacy V2 API key with no owner scope
                    || user.KeyIsSelfOrOrganization(ownerKey)); // V2 API key with owner scope
        }

        private static bool KeyIsSelfOrOrganization(this User user, int? accountKey)
        {
            return user.Key == accountKey
                || user.Organizations.Any(o => o.OrganizationKey == accountKey);
        }

        public static void SetAccountAsDeleted(this User user)
        {
            user.EmailAddress = null;
            user.UnconfirmedEmailAddress = null;
            user.EmailAllowed = false;
            user.EmailConfirmationToken = null;
            user.PasswordResetToken = null;
            user.NotifyPackagePushed = false;
            user.LastFailedLoginUtc = null;
            user.FailedLoginCount = 0;
            user.IsDeleted = true;
        }

        public static bool IsRestrictedToOrganizationTenantPolicy(this User user)
        {
            return user.SecurityPolicies.Any(sp => string.Equals(sp.Name, RequireOrganizationTenantPolicy.PolicyName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
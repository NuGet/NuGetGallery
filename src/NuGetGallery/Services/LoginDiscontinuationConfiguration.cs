// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public class LoginDiscontinuationConfiguration : ILoginDiscontinuationConfiguration
    {
        public bool IsPasswordDiscontinuedForAll { get; }
        public HashSet<string> DiscontinuedForEmailAddresses { get; }
        public HashSet<string> DiscontinuedForDomains { get; }
        public HashSet<string> ExceptionsForEmailAddresses { get; }
        public HashSet<string> ForceTransformationToOrganizationForEmailAddresses { get; }
        public HashSet<OrganizationTenantPair> EnabledOrganizationAadTenants { get; }

        public LoginDiscontinuationConfiguration()
            : this(Enumerable.Empty<string>(),
                  Enumerable.Empty<string>(),
                  Enumerable.Empty<string>(),
                  Enumerable.Empty<string>(),
                  Enumerable.Empty<OrganizationTenantPair>(),
                  isPasswordDiscontinuedForAll: false)
        {
        }

        [JsonConstructor]
        public LoginDiscontinuationConfiguration(
            IEnumerable<string> discontinuedForEmailAddresses,
            IEnumerable<string> discontinuedForDomains,
            IEnumerable<string> exceptionsForEmailAddresses,
            IEnumerable<string> forceTransformationToOrganizationForEmailAddresses,
            IEnumerable<OrganizationTenantPair> enabledOrganizationAadTenants,
            bool isPasswordDiscontinuedForAll)
        {
            DiscontinuedForEmailAddresses = new HashSet<string>(discontinuedForEmailAddresses, StringComparer.OrdinalIgnoreCase);
            DiscontinuedForDomains = new HashSet<string>(discontinuedForDomains, StringComparer.OrdinalIgnoreCase);
            ExceptionsForEmailAddresses = new HashSet<string>(exceptionsForEmailAddresses, StringComparer.OrdinalIgnoreCase);
            ForceTransformationToOrganizationForEmailAddresses = new HashSet<string>(forceTransformationToOrganizationForEmailAddresses, StringComparer.OrdinalIgnoreCase);
            EnabledOrganizationAadTenants = new HashSet<OrganizationTenantPair>(enabledOrganizationAadTenants);
            IsPasswordDiscontinuedForAll = isPasswordDiscontinuedForAll;
        }

        public bool IsLoginDiscontinued(AuthenticatedUser authUser)
        {
            if (authUser == null || authUser.User == null)
            {
                return false;
            }

            var email = authUser.User.ToMailAddress();
            return
                authUser.CredentialUsed.IsPassword() &&
                (IsPasswordDiscontinuedForAll || IsUserOnWhitelist(authUser.User)) &&
                !ExceptionsForEmailAddresses.Contains(email.Address);
        }

        public bool IsUserOnWhitelist(User user)
        {
            if (user == null)
            {
                return false;
            }

            var email = user.ToMailAddress();
            return
                DiscontinuedForDomains.Contains(email.Host) ||
                DiscontinuedForEmailAddresses.Contains(email.Address);
        }

        public bool ShouldUserTransformIntoOrganization(User user)
        {
            if (user == null)
            {
                return false;
            }

            var email = user.ToMailAddress();
            return ForceTransformationToOrganizationForEmailAddresses.Contains(email.Address);
        }

        public bool IsTenantIdPolicySupportedForOrganization(string emailAddress, string tenantId)
        {
            if (string.IsNullOrEmpty(emailAddress))
            {
                return false;
            }

            if (string.IsNullOrEmpty(tenantId))
            {
                return false;
            }

            return EnabledOrganizationAadTenants.Contains(new OrganizationTenantPair(new MailAddress(emailAddress).Host, tenantId));
        }

        public bool IsPasswordLoginDiscontinuedForAll()
        {
            return IsPasswordDiscontinuedForAll;
        }
    }

    public interface ILoginDiscontinuationConfiguration
    {
        bool IsLoginDiscontinued(AuthenticatedUser authUser);
        bool IsPasswordLoginDiscontinuedForAll();
        bool IsUserOnWhitelist(User user);
        bool ShouldUserTransformIntoOrganization(User user);
        bool IsTenantIdPolicySupportedForOrganization(string emailAddress, string tenantId);
    }

    public class OrganizationTenantPair : IEquatable<OrganizationTenantPair>
    {
        public string EmailDomain { get; }
        public string TenantId { get; }

        [JsonConstructor]
        public OrganizationTenantPair(string emailDomain, string tenantId)
        {
            EmailDomain = emailDomain ?? throw new ArgumentNullException(nameof(emailDomain));
            TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as OrganizationTenantPair);
        }

        public bool Equals(OrganizationTenantPair other)
        {
            return other != null &&
                string.Equals(EmailDomain, other.EmailDomain, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(TenantId, other.TenantId, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            var hashCode = -1334890813;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(EmailDomain.ToLowerInvariant());
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TenantId.ToLowerInvariant());
            return hashCode;
        }
    }
}
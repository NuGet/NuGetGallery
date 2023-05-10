// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery.Login
{
    public class LoginDiscontinuation
    {
        public bool IsPasswordDiscontinuedForAll { get; }
        public HashSet<string> DiscontinuedForEmailAddresses { get; set; }
        public HashSet<string> DiscontinuedForDomains { get; }
        public HashSet<string> ExceptionsForEmailAddresses { get; set; }
        public HashSet<string> ForceTransformationToOrganizationForEmailAddresses { get; }
        public HashSet<OrganizationTenantPair> EnabledOrganizationAadTenants { get; }
        
        public LoginDiscontinuation(
            ICollection<string> discontinuedForEmailAddresses,
            ICollection<string> discontinuedForDomains,
            ICollection<string> exceptionsForEmailAddresses,
            ICollection<string> forceTransformationToOrganizationForEmailAddresses,
            ICollection<OrganizationTenantPair> enabledOrganizationAadTenants,
            bool isPasswordDiscontinuedForAll)
        {
            DiscontinuedForEmailAddresses = new HashSet<string>(discontinuedForEmailAddresses, StringComparer.OrdinalIgnoreCase);
            DiscontinuedForDomains = new HashSet<string>(discontinuedForDomains, StringComparer.OrdinalIgnoreCase);
            ExceptionsForEmailAddresses = new HashSet<string>(exceptionsForEmailAddresses, StringComparer.OrdinalIgnoreCase);
            ForceTransformationToOrganizationForEmailAddresses = new HashSet<string>(forceTransformationToOrganizationForEmailAddresses, StringComparer.OrdinalIgnoreCase);
            EnabledOrganizationAadTenants = new HashSet<OrganizationTenantPair>(enabledOrganizationAadTenants);
            IsPasswordDiscontinuedForAll = isPasswordDiscontinuedForAll;
        }

        public bool IsUserInAllowList(User user)
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
                throw new ArgumentException(nameof(emailAddress));
            }

            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentException(nameof(tenantId));
            }

            return EnabledOrganizationAadTenants.Contains(new OrganizationTenantPair(new System.Net.Mail.MailAddress(emailAddress).Host, tenantId));
        }

        public bool IsPasswordLoginDiscontinuedForAll()
        {
            return IsPasswordDiscontinuedForAll;
        }

        public bool IsEmailInExceptionsList(string emailAddress)
        {
            if (string.IsNullOrEmpty(emailAddress))
            {
                return false;
            }

            return ExceptionsForEmailAddresses.Contains(emailAddress);
        }

        public bool AddEmailToExceptionsList(string emailAddress)
        { 
            if (string.IsNullOrEmpty(emailAddress))
            { 
                return false;
            }
            return ExceptionsForEmailAddresses.Add(emailAddress);
        }
        
        public bool RemoveEmailFromExceptionsList(string emailAddress)
        { 
            if (string.IsNullOrEmpty(emailAddress))
            { 
                return false;
            }
            return ExceptionsForEmailAddresses.Remove(emailAddress);
        }
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

        /// <remarks>
        /// Autogenerated by "Quick Actions and Refactoring" -> "Generate Equals and GetHashCode".
        /// </remarks>
        public override int GetHashCode()
        {
            var hashCode = -1334890813;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(EmailDomain.ToLowerInvariant());
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TenantId.ToLowerInvariant());
            return hashCode;
        }
    }
}

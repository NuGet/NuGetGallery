// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// With the addition of organizations, the users table effectively becomes an account table. Organizations accounts
    /// are child types created using TPT inheritance. User accounts are not child types, but this could be done in the
    /// future if we want to add constraints for user accounts or user-only settings.
    /// </summary>
    /// <see href="https://weblogs.asp.net/manavi/inheritance-mapping-strategies-with-entity-framework-code-first-ctp5-part-2-table-per-type-tpt" />
    public class User : IEntity
    {
        public User() : this(null)
        {
        }

        public User(string username)
        {
            Credentials = new List<Credential>();
            SecurityPolicies = new List<UserSecurityPolicy>();
            ReservedNamespaces = new HashSet<ReservedNamespace>();
            Organizations = new List<Membership>();
            Roles = new List<Role>();
            Username = username;
        }

        /// <summary>
        /// Organization memberships for a non-organization <see cref="User"/> account.
        /// </summary>
        public virtual ICollection<Membership> Organizations { get; set; }

        [StringLength(256)]
        public string EmailAddress { get; set; }

        [StringLength(256)]
        public string UnconfirmedEmailAddress { get; set; }

        public virtual ICollection<EmailMessage> Messages { get; set; }

        [StringLength(64)]
        [Required]
        public string Username { get; set; }

        public virtual ICollection<Role> Roles { get; set; }

        public bool EmailAllowed { get; set; }

        public bool IsDeleted { get; set; }

        public virtual ICollection<ReservedNamespace> ReservedNamespaces { get; set; }

        [DefaultValue(true)]
        public bool NotifyPackagePushed { get; set; }

        public bool Confirmed
        {
            get { return !String.IsNullOrEmpty(EmailAddress); }
        }

        [StringLength(32)]
        public string EmailConfirmationToken { get; set; }

        [StringLength(32)]
        public string PasswordResetToken { get; set; }

        public DateTime? PasswordResetTokenExpirationDate { get; set; }

        public int Key { get; set; }

        public DateTime? CreatedUtc { get; set; }

        public DateTime? LastFailedLoginUtc { get; set; }

        public int FailedLoginCount { get; set; }

        public string LastSavedEmailAddress
        {
            get
            {
                return UnconfirmedEmailAddress ?? EmailAddress;
            }
        }

        public virtual ICollection<Credential> Credentials { get; set; }

        public virtual ICollection<UserSecurityPolicy> SecurityPolicies { get; set; }

        public void ConfirmEmailAddress()
        {
            if (string.IsNullOrEmpty(UnconfirmedEmailAddress))
            {
                throw new InvalidOperationException("User does not have an email address to confirm");
            }
            EmailAddress = UnconfirmedEmailAddress;
            EmailConfirmationToken = null;
            UnconfirmedEmailAddress = null;
        }

        public void CancelChangeEmailAddress()
        {
            EmailConfirmationToken = null;
            UnconfirmedEmailAddress = null;
        }

        public void UpdateEmailAddress(string newEmailAddress, Func<string> generateToken)
        {
            if (!string.IsNullOrEmpty(UnconfirmedEmailAddress))
            {
                if (string.Equals(UnconfirmedEmailAddress, newEmailAddress, StringComparison.Ordinal))
                {
                    return; // already set as latest (unconfirmed) email address
                }
            }
            else
            {
                if (String.Equals(EmailAddress, newEmailAddress, StringComparison.Ordinal))
                {
                    return; // already set as latest (confirmed) email address
                }
            }

            UnconfirmedEmailAddress = newEmailAddress;
            EmailConfirmationToken = generateToken();
        }

        public bool HasPassword()
        {
            return Credentials.Any(c =>
                c.Type.StartsWith(CredentialTypes.Password.Prefix, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsInRole(string roleName)
        {
            return Roles.Any(r => String.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));
        }

        public User Clone()
        {
            // Creates a copy without referenced Entities, to avoid risk of
            // circular clones.
            return new User()
            {
                CreatedUtc = CreatedUtc,
                EmailAddress = EmailAddress,
                EmailAllowed = EmailAllowed,
                EmailConfirmationToken = EmailConfirmationToken,
                FailedLoginCount = FailedLoginCount,
                IsDeleted = IsDeleted,
                Key = Key,
                LastFailedLoginUtc = LastFailedLoginUtc,
                NotifyPackagePushed = NotifyPackagePushed,
                PasswordResetToken = PasswordResetToken,
                PasswordResetTokenExpirationDate = PasswordResetTokenExpirationDate,
                UnconfirmedEmailAddress = UnconfirmedEmailAddress,
                Username = Username
            };
        }
    }
}

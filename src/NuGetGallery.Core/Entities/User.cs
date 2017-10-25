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
    /// NuGetGallery now supports both Users and Organizations which share common features such as:
    /// - Namespace ownership
    /// - Package ownership
    /// - Security policies
    /// - Name and email unique across both Users and Organizations
    /// 
    /// Organizations should not support UserRoles or Credentials, but this is not constrained by the
    /// database. In the future, we could consider renaming the Users table to Accounts and adding a
    /// Users table to constrain UserRoles and Credentials to only User accounts.
    /// </summary>
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
            Roles = new List<Role>();
            Username = username;
        }

        /// <summary>
        /// <see cref="Organization"/> represented by this account, if any.
        /// </summary>
        public Organization Organization { get; set; }
        
        /// <summary>
        /// Whether this is an <see cref="Organization"/> account.
        /// </summary>
        public bool IsOrganization
        {
            get
            {
                return Organization != null;
            }
        }

        /// <summary>
        /// Organization memberships for a <see cref="User"/> account.
        /// </summary>
        public virtual ICollection<Membership> Memberships { get; set; }

        /// <summary>
        /// Organizations in which the <see cref="User"/> is a member.
        /// </summary>
        public IEnumerable<Organization> Organizations
        {
            get
            {
                return Memberships.Select(m => m.Organization);
            }
        }

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
    }
}

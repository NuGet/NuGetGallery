// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// With the addition of organizations, the users table effectively becomes an account table. Organizations accounts
    /// are child types created using TPT inheritance. User accounts are not child types, but this could be done in the
    /// future if we want to add constraints for user accounts or user-only settings.
    /// </summary>
    /// <see href="https://weblogs.asp.net/manavi/inheritance-mapping-strategies-with-entity-framework-code-first-ctp5-part-2-table-per-type-tpt" />
    public class User : IEntity, IEquatable<User>
    {
        #region per-request query cache

        private bool? _isAdmin;

        [NotMapped]
        public bool IsAdministrator
        {
            get
            {
                if (!_isAdmin.HasValue)
                {
                    _isAdmin = IsInRole(Constants.AdminRoleName);
                }
                return _isAdmin.Value;
            }
        }

        #endregion

        public User() : this(null)
        {
        }

        public User(string username)
        {
            Credentials = new List<Credential>();
            SecurityPolicies = new List<UserSecurityPolicy>();
            ReservedNamespaces = new HashSet<ReservedNamespace>();
            Organizations = new List<Membership>();
            OrganizationMigrationRequests = new List<OrganizationMigrationRequest>();
            OrganizationRequests = new List<MembershipRequest>();
            Roles = new List<Role>();
            Username = username;
            UserCertificates = new List<UserCertificate>();
        }

        /// <summary>
        /// Organization memberships, for a non-organization <see cref="User"/> account.
        /// </summary>
        public virtual ICollection<Membership> Organizations { get; set; }

        /// <summary>
        /// Organization membership requests, for a non-organization <see cref="User"/> account.
        /// </summary>
        public virtual ICollection<MembershipRequest> OrganizationRequests { get; set; }

        /// <summary>
        /// Request to transform a <see cref="User"/> account into an <see cref="Organization"/> account.
        /// </summary>
        public virtual OrganizationMigrationRequest OrganizationMigrationRequest { get; set; }

        /// <summary>
        /// Requests for this user to become the admin of a <see cref="User"/> account that was transformed into an <see cref="Organization"/> account.
        /// </summary>
        public virtual ICollection<OrganizationMigrationRequest> OrganizationMigrationRequests { get; set; }

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

        public bool EnableMultiFactorAuthentication { get; set; }

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

        /// <summary>
        /// Gets or sets the collection of user certificates.
        /// </summary>
        public virtual ICollection<UserCertificate> UserCertificates { get; set; }

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

        public void UpdateUnconfirmedEmailAddress(string newEmailAddress, Func<string> generateToken)
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

        public bool IsInRole(string roleName)
        {
            return Roles.Any(r => String.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));
        }

        public bool Equals(User other)
        {
            return other.Key == Key;
        }

        public override bool Equals(object obj)
        {
            if(obj == null)
            {
                return false;
            }
            User user = obj as User;
            if(user == null)
            {
                return false;
            }
            return Equals(user);
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public static bool operator ==(User user1, User user2)
        {
            if (((object)user1) == null || ((object)user2) == null)
            {
                return Equals(user1, user2);
            }

            return user1.Equals(user2);
        }

        public static bool operator !=(User user1, User user2)
        {
            if (((object)user1) == null || ((object)user2) == null)
            {
                return !Equals(user1, user2);
            }

            return !user1.Equals(user2);
        }
    }
}

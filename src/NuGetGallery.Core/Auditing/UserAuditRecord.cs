// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class UserAuditRecord : AuditRecord<AuditedUserAction>
    {
        public string Username { get; }
        public string EmailAddress { get; }
        public string UnconfirmedEmailAddress { get; }
        public string[] Roles { get; }
        public CredentialAuditRecord[] Credentials { get; }

        /// <summary>
        /// The credential affected by <see cref="AuditRecord{T}.Action"/>.
        /// </summary>
        public CredentialAuditRecord[] AffectedCredential { get; }

        /// <summary>
        /// The email address affected by <see cref="AuditRecord{T}.Action"/>.
        /// </summary>
        public string AffectedEmailAddress { get; }

        /// <summary>
        /// The username of the member affected by <see cref="AuditRecord{T}.Action"/>.
        /// </summary>
        public string AffectedMemberUsername { get; }

        /// <summary>
        /// Whether or not the member affected by <see cref="AuditRecord{T}.Action"/> is an administrator or not.
        /// </summary>
        public bool AffectedMemberIsAdmin { get; }

        /// <summary>
        /// Subset of user policies affected by <see cref="AuditRecord{T}.Action"/>.
        /// </summary>
        public AuditedUserSecurityPolicy[] AffectedPolicies { get; }

        public UserAuditRecord(User user, AuditedUserAction action)
            : base(action)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Username = user.Username;
            EmailAddress = user.EmailAddress;
            UnconfirmedEmailAddress = user.UnconfirmedEmailAddress;
            Roles = user.Roles.Select(r => r.Name).ToArray();

            Credentials = user.Credentials.Where(CredentialTypes.IsSupportedCredential)
                                          .Select(c => new CredentialAuditRecord(c, removedOrRevoked: false)).ToArray();

            AffectedCredential = Array.Empty<CredentialAuditRecord>();
            AffectedPolicies = Array.Empty<AuditedUserSecurityPolicy>();
        }

        public UserAuditRecord(User user, AuditedUserAction action, Credential affected, string revocationSource)
            : this(user, action, new[] { affected }, revocationSource)
        {
        }

        public UserAuditRecord(User user, AuditedUserAction action, IEnumerable<Credential> affected, string revocationSource)
            : this(user, action)
        {
            AffectedCredential = affected.Select(c => new CredentialAuditRecord(c,
                removedOrRevoked: action == AuditedUserAction.RemoveCredential || action == AuditedUserAction.RevokeCredential, revocationSource: revocationSource)).ToArray();
        }

        public UserAuditRecord(User user, AuditedUserAction action, Credential affected)
            : this(user, action, new[] { affected })
        {
        }

        public UserAuditRecord(User user, AuditedUserAction action, IEnumerable<Credential> affected)
            : this(user, action)
        {
            AffectedCredential = affected.Select(c => new CredentialAuditRecord(c,
                removedOrRevoked: action == AuditedUserAction.RemoveCredential || action == AuditedUserAction.RevokeCredential)).ToArray();
        }

        public UserAuditRecord(User user, AuditedUserAction action, string affectedEmailAddress)
            : this(user, action)
        {
            AffectedEmailAddress = affectedEmailAddress;
        }

        public UserAuditRecord(User user, AuditedUserAction action, User affectedMember, bool affectedMemberIsAdmin)
            : this(user, action)
        {
            AffectedMemberUsername = affectedMember.Username;
            AffectedMemberIsAdmin = affectedMemberIsAdmin;
        }

        public UserAuditRecord(User user, AuditedUserAction action, Membership affectedMembership)
            : this(user, action, affectedMembership.Member, affectedMembership.IsAdmin)
        {
        }

        public UserAuditRecord(User user, AuditedUserAction action, IEnumerable<UserSecurityPolicy> affectedPolicies)
            : this(user, action)
        {
            if (affectedPolicies == null || affectedPolicies.Count() == 0)
            {
                throw new ArgumentException(nameof(affectedPolicies));
            }

            AffectedPolicies = affectedPolicies.Select(p => new AuditedUserSecurityPolicy(p)).ToArray();
        }

        public override string GetPath()
        {
            return Username.ToLowerInvariant();
        }
    }
}

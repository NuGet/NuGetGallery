// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        public CredentialAuditRecord[] AffectedCredential { get; }
        public string AffectedEmailAddress { get; }
        public string AffectedMemberUsername { get; }
        public bool AffectedMemberIsAdmin { get; }

        /// <summary>
        /// Subset of user policies affected by the action (subscription / unsubscription).
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
                                          .Select(c => new CredentialAuditRecord(c, removed: false)).ToArray();

            AffectedCredential = new CredentialAuditRecord[0];
            AffectedPolicies = new AuditedUserSecurityPolicy[0];
        }

        public UserAuditRecord(User user, AuditedUserAction action, Credential affected)
            : this(user, action, new[] { affected })
        {
        }

        public UserAuditRecord(User user, AuditedUserAction action, IEnumerable<Credential> affected)
            : this(user, action)
        {
            AffectedCredential = affected.Select(c => new CredentialAuditRecord(c, action == AuditedUserAction.RemoveCredential)).ToArray();
        }

        public UserAuditRecord(User user, AuditedUserAction action, string affectedEmailAddress)
            : this(user, action)
        {
            AffectedEmailAddress = affectedEmailAddress;
        }

        public UserAuditRecord(User user, AuditedUserAction action, Membership affectedMembership)
            : this(user, action)
        {
            AffectedMemberUsername = affectedMembership.Member.Username;
            AffectedMemberIsAdmin = affectedMembership.IsAdmin;
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

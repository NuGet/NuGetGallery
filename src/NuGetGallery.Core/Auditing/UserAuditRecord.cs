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

        /// <summary>
        /// Subset of user policies affected by the action (subscription / unsubscription).
        /// </summary>
        public AuditedUserSecurityPolicy[] AffectedPolicies { get; }

        public UserAuditRecord(User user, AuditedUserAction action)
            : this(user, action, Enumerable.Empty<Credential>())
        {
        }

        public UserAuditRecord(User user, AuditedUserAction action, Credential affected)
            : this(user, action, SingleEnumerable(affected))
        {
        }

        public UserAuditRecord(User user, AuditedUserAction action, IEnumerable<Credential> affected)
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

            if (affected != null)
            {
                AffectedCredential = affected.Select(c => new CredentialAuditRecord(c, action == AuditedUserAction.RemoveCredential)).ToArray();
            }

            Action = action;
        }
        
        public UserAuditRecord(User user, AuditedUserAction action, string affectedEmailAddress)
            : this(user, action, Enumerable.Empty<Credential>())
        {
            AffectedEmailAddress = affectedEmailAddress;
        }

        public UserAuditRecord(User user, AuditedUserAction action, IEnumerable<UserSecurityPolicy> affectedPolicies)
            : this(user, action, Enumerable.Empty<Credential>())
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

        private static IEnumerable<Credential> SingleEnumerable(Credential affected)
        {
            yield return affected;
        }
    }
}

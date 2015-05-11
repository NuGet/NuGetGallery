// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NuGetGallery.Auditing
{
    public class UserAuditRecord : AuditRecord<UserAuditAction>
    {
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public string UnconfirmedEmailAddress { get; set; }
        public string[] Roles { get; set; }
        public CredentialAuditRecord[] Credentials { get; set; }
        public CredentialAuditRecord[] AffectedCredential { get; set; }
        public string AffectedEmailAddress { get; set; }
        
        public UserAuditRecord(User user, UserAuditAction action)
            : this(user, action, Enumerable.Empty<Credential>()) { }
        public UserAuditRecord(User user, UserAuditAction action, Credential affected)
            : this(user, action, SingleEnumerable(affected)) { }
        public UserAuditRecord(User user, UserAuditAction action, IEnumerable<Credential> affected)
            : base(action)
        {
            Username = user.Username;
            EmailAddress = user.EmailAddress;
            UnconfirmedEmailAddress = user.UnconfirmedEmailAddress;
            Roles = user.Roles.Select(r => r.Name).ToArray();
            Credentials = user.Credentials.Select(c => new CredentialAuditRecord(c, removed: false)).ToArray();

            if (affected != null)
            {
                AffectedCredential = affected.Select(c => new CredentialAuditRecord(c, action == UserAuditAction.RemovedCredential)).ToArray();
            }

            Action = action;
        }
        
        public UserAuditRecord(User user, UserAuditAction action, string affectedEmailAddress)
            : this(user, action, Enumerable.Empty<Credential>()) {
            AffectedEmailAddress = affectedEmailAddress;
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

    public class CredentialAuditRecord
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public string Identity { get; set; }

        public CredentialAuditRecord(Credential credential, bool removed)
        {
            Type = credential.Type;
            Identity = credential.Identity;

            // Track the value for credentials that are definitely revokable (API Key, etc.) and have been removed
            if (removed && !CredentialTypes.IsPassword(credential.Type))
            {
                Value = credential.Value;
            }
        }
    }

    public enum UserAuditAction
    {
        Registered,
        AddedCredential,
        RemovedCredential,
        RequestedPasswordReset,
        ChangeEmail,
        CancelChangeEmail,
        ConfirmEmail,
    }
}

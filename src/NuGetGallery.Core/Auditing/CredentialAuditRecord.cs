// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.Auditing
{
    public class CredentialAuditRecord
    {
        public int Key { get; }
        public string Type { get; }
        public string Value { get; }
        public string Description { get; }
        public List<ScopeAuditRecord> Scopes { get; set; }
        public string Identity { get; }
        public DateTime Created { get; }
        public DateTime? Expires { get; }
        public DateTime? LastUsed { get; }
        public string TenantId { get; }
        public string RevocationSource { get; }

        public CredentialAuditRecord(Credential credential)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            Key = credential.Key;
            Type = credential.Type;
            Description = credential.Description;
            Identity = credential.Identity;
            TenantId = credential.TenantId;

            // Track the value for credentials that are external (object id)
            // Do not track the credential valid for API keys or passwords, even if they are revoked.
            if (credential.IsExternal())
            {
                Value = credential.Value;
            }

            Created = credential.Created;
            Expires = credential.Expires;
            LastUsed = credential.LastUsed;

            // Track scopes
            Scopes = new List<ScopeAuditRecord>();
            foreach (var scope in credential.Scopes)
            {
                var ownerScope = scope.Owner?.Username;
                Scopes.Add(new ScopeAuditRecord(ownerScope, scope.Subject, scope.AllowedAction));
            }
        }

        public CredentialAuditRecord(Credential credential, string revocationSource)
                : this(credential)
        {
            RevocationSource = revocationSource;
        }
    }
}

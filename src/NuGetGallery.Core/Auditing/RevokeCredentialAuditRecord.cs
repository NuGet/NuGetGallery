// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery.Auditing
{
    public class RevokeCredentialAuditRecord : AuditRecord<AuditedRevokeCredentialAction>
    {
        public string Username { get; }
        public string RequestingUsername { get; }
        public CredentialAuditRecord Credential { get; }

        public RevokeCredentialAuditRecord(Credential credential, AuditedRevokeCredentialAction action, string requestingUsername)
            : base(action)
        {
            Username = credential.User.Username;
            RequestingUsername = requestingUsername;
            Credential = new CredentialAuditRecord(credential, removed: false);
        }

        public override string GetPath()
        {
            return Username.ToLowerInvariant();
        }
    }
}
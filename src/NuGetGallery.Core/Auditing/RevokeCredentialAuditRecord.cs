// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery.Auditing
{
    public class RevokeCredentialAuditRecord : AuditRecord<AuditedRevokeCredentialAction>
    {
        public string Username { get; }
        public string RevocationSource { get; }
        public string LeakedUrl { get; }
        public CredentialAuditRecord Credential { get; }
        public string RequestingUsername { get; }

        public RevokeCredentialAuditRecord(Credential credential,
            AuditedRevokeCredentialAction action,
            string revocationSource,
            string leakedUrl,
            string requestingUsername) : base(action)
        {
            Username = credential.User.Username;
            RevocationSource = revocationSource;
            LeakedUrl = leakedUrl;
            Credential = new CredentialAuditRecord(credential, removed: false);
            RequestingUsername = requestingUsername;
        }

        public override string GetPath()
        {
            return Username.ToLowerInvariant();
        }
    }
}
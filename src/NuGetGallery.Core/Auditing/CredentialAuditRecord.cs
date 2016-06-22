// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public class CredentialAuditRecord
    {
        public int Key { get; }
        public string Type { get; }
        public string Value { get; }
        public string Identity { get; }

        public CredentialAuditRecord(Credential credential, bool removed)
        {
            Key = credential.Key;
            Type = credential.Type;
            Identity = credential.Identity;

            // Track the value for credentials that are definitely revokable (API Key, etc.) and have been removed
            if (removed && !CredentialTypes.IsPassword(credential.Type))
            {
                Value = credential.Value;
            }
        }
    }
}
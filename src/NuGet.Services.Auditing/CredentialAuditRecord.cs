﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Gallery;
using NuGet.Services.Gallery.Entities;

namespace NuGet.Services.Auditing
{
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
}
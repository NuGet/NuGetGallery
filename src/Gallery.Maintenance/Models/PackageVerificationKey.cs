// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Gallery.Maintenance.Models
{
    public class PackageVerificationKey
    {
        public int CredentialKey { get; set; }

        public int UserKey { get; set; }

        public string Username { get; set; }

        public DateTime Expires { get; set; }

        public string ScopeSubject { get; set; }
    }
}

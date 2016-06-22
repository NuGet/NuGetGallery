// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Gallery.CredentialExpiration.Models
{
    public class ExpiredCredentialData
    {
        public string Type { get; set; }
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Expires { get; set; }
    }
}

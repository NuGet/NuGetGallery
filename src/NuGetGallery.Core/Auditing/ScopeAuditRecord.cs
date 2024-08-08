// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public class ScopeAuditRecord(string ownerUsername, string subject, string allowedAction)
    {
        public string OwnerUsername { get; set; } = ownerUsername;
        public string Subject { get; set; } = subject;
        public string AllowedAction { get; set; } = allowedAction;
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public class ScopeAuditRecord
    {
        public string OwnerUsername { get; set; }
        public string Subject { get; set; }
        public string AllowedAction { get; set; }

        public ScopeAuditRecord(string ownerUsername, string subject, string allowedAction)
        {
            OwnerUsername = ownerUsername;
            Subject = subject;
            AllowedAction = allowedAction;
        }
    }
}
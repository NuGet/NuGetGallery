// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// Represents the actual data stored in an audit entry, an AuditRecord/AuditEnvironment pair
    /// </summary>
    public class AuditEntry
    {
        public AuditRecord Record { get; set; }
        public AuditActor Actor { get; set; }

        public AuditEntry(AuditRecord record, AuditActor actor)
        {
            Record = record;
            Actor = actor;
        }
    }
}

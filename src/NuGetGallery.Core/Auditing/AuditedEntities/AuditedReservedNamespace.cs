// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedReservedNamespace
    {
        public string Value { get; }
        public bool IsPrefix { get; }
        public bool IsSharedNamespace { get; }

        public AuditedReservedNamespace(ReservedNamespace reservedNamespace)
        {
            Value = reservedNamespace.Value;
            IsSharedNamespace = reservedNamespace.IsSharedNamespace;
            IsPrefix = reservedNamespace.IsPrefix;
        }
    }
}
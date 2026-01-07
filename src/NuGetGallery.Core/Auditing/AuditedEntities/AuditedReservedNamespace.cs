// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedReservedNamespace
    {
        public string Value { get; private set; }
        public bool IsPrefix { get; private set; }
        public bool IsSharedNamespace { get; private set; }

        public static AuditedReservedNamespace CreateFrom(ReservedNamespace reservedNamespace)
        {
            return new AuditedReservedNamespace
            {
                Value = reservedNamespace.Value,
                IsSharedNamespace = reservedNamespace.IsSharedNamespace,
                IsPrefix = reservedNamespace.IsPrefix,
            };
        }
    }
}
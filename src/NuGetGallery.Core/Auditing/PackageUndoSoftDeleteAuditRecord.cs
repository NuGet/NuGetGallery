// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// Audit record for undoing a package soft deletion.
    /// </summary>
    public class PackageUndoSoftDeleteAuditRecord : PackageAuditRecord
    {
        public int PackageKey { get; }

        public int PackageDeleteKey { get; }

        public bool Success { get; }

        public PackageUndoSoftDeleteAuditRecord(Package package, int packageDeleteKey, string reason, bool success, string callerIdentity = null)
            : base(package ?? throw new ArgumentNullException(nameof(package)), AuditedPackageAction.UndoSoftDelete, reason, callerIdentity)
        {
            if (package.Key <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(package));
            }

            if (packageDeleteKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packageDeleteKey));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A soft-delete undo reason is required.", nameof(reason));
            }

            PackageKey = package.Key;
            PackageDeleteKey = packageDeleteKey;
            Success = success;
        }

        // Store alongside other package audit records.
        public override string GetResourceType() => "Package";
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;

namespace NuGetGallery.Auditing
{
    public class ReservedNamespaceAuditRecord : AuditRecord<AuditedReservednamespaceAction>
    {
        public string Value { get; }
        public string IsPrefix { get; }
        public string IsSharedNamespace { get; }
        public PackageRegistrationAuditRecord[] AffectedPackageRegistrations { get; }

        public ReservedNamespaceAuditRecord(ReservedNamespace reservedNamespace, AuditedReservednamespaceAction action)
            : this(reservedNamespace, action, Enumerable.Empty<PackageRegistration>())
        {
        }

        public ReservedNamespaceAuditRecord(ReservedNamespace reservedNamespace, AuditedReservednamespaceAction action, PackageRegistration affected)
            : this(reservedNamespace, action, SingleEnumerable(affected))
        {
        }

        public ReservedNamespaceAuditRecord(ReservedNamespace reservedNamespace, AuditedReservednamespaceAction action, IEnumerable<PackageRegistration> affected)
            : base(action)
        {
            if (reservedNamespace == null)
            {
                throw new ArgumentNullException(nameof(reservedNamespace));
            }

            Action = action;
        }

        private static IEnumerable<PackageRegistration> SingleEnumerable(PackageRegistration affected)
        {
            yield return affected;
        }

        public override string GetPath()
        {
            return Value.ToLowerInvariant();
        }
    }
}

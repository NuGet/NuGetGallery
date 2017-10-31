// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class ReservedNamespaceAuditRecord : AuditRecord<AuditedReservednamespaceAction>
    {
        public string Value;

        public AuditedReservedNamespace AffectedReservedNamespace;

        public string AffectedOwner;

        public PackageRegistrationAuditRecord[] AffectedRegistrations;

        public ReservedNamespaceAuditRecord(ReservedNamespace reservedNamespace, AuditedReservednamespaceAction action)
            : this(reservedNamespace, action, username: null, registrations: null)
        { }

        public ReservedNamespaceAuditRecord(ReservedNamespace reservedNamespace,
            AuditedReservednamespaceAction action,
            string username,
            IEnumerable<PackageRegistration> registrations)
            : base(action)
        {
            if (reservedNamespace == null)
            {
                throw new ArgumentNullException(nameof(reservedNamespace));
            }

            Value = reservedNamespace.Value;
            AffectedReservedNamespace = new AuditedReservedNamespace(reservedNamespace);
            Action = action;

            if (!string.IsNullOrWhiteSpace(username))
            {
                AffectedOwner = username;

                var registrationAction = GetPackageRegistrationAction(action);
                if (registrations != null && registrations.Any() && registrationAction.HasValue)
                {
                    AffectedRegistrations = registrations
                        .Select(pr => new PackageRegistrationAuditRecord(pr, registrationAction.Value, username))
                        .ToArray();
                }
            }
        }

        public override string GetPath()
        {
            return Value.ToLowerInvariant();
        }

        private AuditedPackageRegistrationAction? GetPackageRegistrationAction(AuditedReservednamespaceAction action)
        {
            switch (action)
            {
                case AuditedReservednamespaceAction.AddOwner:
                    return AuditedPackageRegistrationAction.MarkVerified;
                case AuditedReservednamespaceAction.RemoveOwner:
                    return AuditedPackageRegistrationAction.MarkUnverified;
                default:
                    return null;
            }
        }
    }
}

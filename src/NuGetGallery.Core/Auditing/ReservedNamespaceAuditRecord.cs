// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class ReservedNamespaceAuditRecord : AuditRecord<AuditedReservedNamespaceAction>
    {
        public string Value;

        public AuditedReservedNamespace AffectedReservedNamespace;

        public string AffectedOwner;

        public PackageRegistrationAuditRecord[] AffectedRegistrations;

        public ReservedNamespaceAuditRecord(ReservedNamespace reservedNamespace, AuditedReservedNamespaceAction action)
            : this(reservedNamespace, action, username: null, registrations: null)
        { }

        public ReservedNamespaceAuditRecord(ReservedNamespace reservedNamespace,
            AuditedReservedNamespaceAction action,
            string username,
            IEnumerable<PackageRegistration> registrations)
            : base(action)
        {
            if (reservedNamespace == null)
            {
                throw new ArgumentNullException(nameof(reservedNamespace));
            }

            Value = reservedNamespace.Value;
            AffectedReservedNamespace = AuditedReservedNamespace.CreateFrom(reservedNamespace);
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

        private static AuditedPackageRegistrationAction? GetPackageRegistrationAction(AuditedReservedNamespaceAction action)
        {
            switch (action)
            {
                case AuditedReservedNamespaceAction.AddOwner:
                    return AuditedPackageRegistrationAction.MarkVerified;
                case AuditedReservedNamespaceAction.RemoveOwner:
                    return AuditedPackageRegistrationAction.MarkUnverified;
                default:
                    return null;
            }
        }
    }
}

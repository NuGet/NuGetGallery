// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Auditing
{
    public class ReservedNamespaceAuditRecord : AuditRecord<AuditedReservedNamespaceAction>
    {
        public string Value;

        public bool IsSharedNamespace;

        public bool IsPrefix;

        public UserAuditRecord AffectedUser;

        public PackageRegistrationAuditRecord[] AffectedRegistrations;

        public ReservedNamespaceAuditRecord(ReservedNamespace prefix, AuditedReservedNamespaceAction action)
            : base(action)
        {
            Value = prefix.Value;
            IsSharedNamespace = prefix.IsSharedNamespace;
            IsPrefix = prefix.IsPrefix;

            Action = action;
        }

        public ReservedNamespaceAuditRecord(
            ReservedNamespace prefix, 
            User user, 
            IEnumerable<PackageRegistration> registrations, 
            AuditedReservedNamespaceAction action)
            : base(action)
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Value = prefix.Value;
            IsSharedNamespace = prefix.IsSharedNamespace;
            IsPrefix = prefix.IsPrefix;

            if (action == AuditedReservedNamespaceAction.AddOwner)
            {
                AffectedUser = new UserAuditRecord(user, AuditedUserAction.GainNamespaceOwnership);
                AffectedRegistrations = registrations
                    .Select(x => new PackageRegistrationAuditRecord(x, AuditedPackageRegistrationAction.AddNamespace, prefix)).ToArray();
            }
            else if (action == AuditedReservedNamespaceAction.DeleteOwner)
            {
               AffectedUser = new UserAuditRecord(user, AuditedUserAction.LoseNamesapceOwnership);
                AffectedRegistrations = registrations
                    .Select(x => new PackageRegistrationAuditRecord(x, AuditedPackageRegistrationAction.RemoveNamespace, prefix)).ToArray();
            }

            Action = action;
        }

        public override string GetPath()
        {
            return Value.ToLowerInvariant();
        }
    }
}

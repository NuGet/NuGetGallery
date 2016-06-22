// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class PackageRegistrationAuditRecord : AuditRecord<AuditedPackageRegistrationAction>
    {
        public string Id { get; }
        public AuditedPackageRegistration RegistrationRecord { get; }
        public string Owner { get; }

        public PackageRegistrationAuditRecord(
            string id, AuditedPackageRegistration registrationRecord, AuditedPackageRegistrationAction action, string owner)
            : base(action)
        {
            Id = id;
            RegistrationRecord = registrationRecord;
            Owner = owner;
        }

        public PackageRegistrationAuditRecord(
            PackageRegistration packageRegistration, AuditedPackageRegistrationAction action, string owner)
            : this(packageRegistration.Id, AuditedPackageRegistration.CreateFrom(packageRegistration), action, owner)
        {
        }
        
        public override string GetPath()
        {
            return $"{Id}".ToLowerInvariant();
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class PackageAuditRecord : AuditRecord<AuditedPackageAction>
    {
        public string Id { get; }
        public string Version { get; }
        public string Hash { get; }

        public AuditedPackage PackageRecord { get; }
        public AuditedPackageRegistration RegistrationRecord { get; }

        public string Reason { get; }

        public PackageAuditRecord(
            string id, string version, string hash,
            AuditedPackage packageRecord, AuditedPackageRegistration registrationRecord, 
            AuditedPackageAction action, string reason)
            : base(action)
        {
            Id = id;
            Version = version;
            Hash = hash;
            PackageRecord = packageRecord;
            RegistrationRecord = registrationRecord;
            Reason = reason;
        }

        public PackageAuditRecord(
            Package package, AuditedPackageAction action, string reason)
            : this(package.PackageRegistration.Id, package.Version, package.Hash, 
                  packageRecord: null, registrationRecord: null, action: action, reason: reason)
        {
            PackageRecord = AuditedPackage.CreateFrom(package);
            RegistrationRecord = AuditedPackageRegistration.CreateFrom(package.PackageRegistration);
        }

        public PackageAuditRecord(Package package, AuditedPackageAction action)
            : this(package.PackageRegistration.Id, package.Version, package.Hash, 
                  packageRecord: null, registrationRecord: null, action: action, reason: null)
        {
            PackageRecord = AuditedPackage.CreateFrom(package);
            RegistrationRecord = AuditedPackageRegistration.CreateFrom(package.PackageRegistration);
        }
        
        public override string GetPath()
        {
            return $"{Id}/{NuGetVersionNormalizer.Normalize(Version)}"
                .ToLowerInvariant();
        }
    }
}
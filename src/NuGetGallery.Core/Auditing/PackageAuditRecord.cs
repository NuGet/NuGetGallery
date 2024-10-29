// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Auditing.AuditedEntities;
using System.Linq;

namespace NuGetGallery.Auditing
{
    public class PackageAuditRecord : AuditRecord<AuditedPackageAction>
    {
        public string Id { get; }
        public string Version { get; }
        public string Hash { get; }

        public AuditedPackage PackageRecord { get; }
        public AuditedPackageRegistration RegistrationRecord { get; }
        public AuditedPackageDeprecation DeprecationRecord { get; }

        public string Reason { get; }

        public PackageAuditRecord(
            string id, string version, string hash,
            AuditedPackage packageRecord,
            AuditedPackageRegistration registrationRecord,
            AuditedPackageDeprecation deprecationRecord,
            AuditedPackageAction action, string reason)
            : base(action)
        {
            Id = id;
            Version = version;
            Hash = hash;
            PackageRecord = packageRecord;
            RegistrationRecord = registrationRecord;
            DeprecationRecord = deprecationRecord;
            Reason = reason;
        }

        public PackageAuditRecord(string id, string version, AuditedPackageAction action, string reason)
            : this(id,
                  version,
                  hash: "",
                  packageRecord: null,
                  registrationRecord: null,
                  deprecationRecord: null,
                  action: action,
                  reason: reason)
        { }

        public PackageAuditRecord(Package package, AuditedPackageAction action, string reason)
            : this(package.PackageRegistration.Id,
                  package.Version,
                  package.Hash,
                  packageRecord: null,
                  registrationRecord: null,
                  deprecationRecord: null,
                  action: action,
                  reason: reason)
        {
            PackageRecord = AuditedPackage.CreateFrom(package);
            RegistrationRecord = AuditedPackageRegistration.CreateFrom(package.PackageRegistration);
            DeprecationRecord = package.Deprecations
                .Select(d => AuditedPackageDeprecation.CreateFrom(d))
                .SingleOrDefault();
        }

        public PackageAuditRecord(Package package, AuditedPackageAction action)
            : this(package.PackageRegistration.Id,
                  package.Version,
                  package.Hash,
                  packageRecord: null,
                  registrationRecord: null,
                  deprecationRecord: null,
                  action: action,
                  reason: null)
        {
            PackageRecord = AuditedPackage.CreateFrom(package);
            RegistrationRecord = AuditedPackageRegistration.CreateFrom(package.PackageRegistration);
            DeprecationRecord = package.Deprecations
                .Select(d => AuditedPackageDeprecation.CreateFrom(d))
                .SingleOrDefault();
        }

        public override string GetPath()
        {
            return $"{Id}/{NuGetVersionFormatter.Normalize(Version)}"
                .ToLowerInvariant();
        }
    }
}
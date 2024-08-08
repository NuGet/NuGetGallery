// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Auditing.AuditedEntities;
using System.Linq;

namespace NuGetGallery.Auditing
{
    public class PackageAuditRecord(
        string id, string version, string hash,
        AuditedPackage packageRecord,
        AuditedPackageRegistration registrationRecord,
        AuditedPackageDeprecation deprecationRecord,
        AuditedPackageAction action, string reason) : AuditRecord<AuditedPackageAction>(action)
    {
        public string Id { get; } = id;
        public string Version { get; } = version;
        public string Hash { get; } = hash;

        public AuditedPackage PackageRecord { get; } = packageRecord;
        public AuditedPackageRegistration RegistrationRecord { get; } = registrationRecord;
        public AuditedPackageDeprecation DeprecationRecord { get; } = deprecationRecord;

        public string Reason { get; } = reason;

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

        public override string GetPath() =>
            $"{Id}/{NuGetVersionFormatter.Normalize(Version)}"
                .ToLowerInvariant();
    }
}
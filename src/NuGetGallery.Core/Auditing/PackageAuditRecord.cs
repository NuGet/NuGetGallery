// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class PackageAuditRecord2 : AuditRecord<AuditedPackageAction>
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }

        public List<AuditedPackage> PackageRecord { get; set; }
        public List<AuditedPackageRegistration> RegistrationRecord { get; set; }

        public string Reason { get; set; }

        public PackageAuditRecord2()
        {

        }
        public PackageAuditRecord2(
            string id, string version, string hash,
            AuditedPackage packageRecord, AuditedPackageRegistration registrationRecord,
            AuditedPackageAction action, string reason)
            : base(action)
        {
            Id = id;
            Version = version;
            Hash = hash;
            PackageRecord = new List<AuditedPackage>() { packageRecord };
            RegistrationRecord = new List<AuditedPackageRegistration>() { registrationRecord };
            Reason = reason;
        }

        public PackageAuditRecord2(string id, string version, AuditedPackageAction action, string reason)
            : this(id,
                  version,
                  hash: "",
                  packageRecord: null,
                  registrationRecord: null,
                  action: action,
                  reason: reason)
        { }

        public PackageAuditRecord2(Package package, AuditedPackageAction action, string reason)
            : this(package.PackageRegistration.Id,
                  package.Version,
                  package.Hash,
                  packageRecord: null,
                  registrationRecord: null,
                  action: action,
                  reason: reason)
        {
            PackageRecord = new List<AuditedPackage> { AuditedPackage.CreateFrom(package) };
            RegistrationRecord = new List<AuditedPackageRegistration> { AuditedPackageRegistration.CreateFrom(package.PackageRegistration) };
        }

        public PackageAuditRecord2(Package package, AuditedPackageAction action)
            : this(package.PackageRegistration.Id,
                  package.Version,
                  package.Hash,
                  packageRecord: null,
                  registrationRecord: null,
                  action: action,
                  reason: null)
        {
            PackageRecord = new List<AuditedPackage> { AuditedPackage.CreateFrom(package) };
            RegistrationRecord = new List<AuditedPackageRegistration> { AuditedPackageRegistration.CreateFrom(package.PackageRegistration) };
        }

        public override string GetPath()
        {
            return $"{Id}/{NuGetVersionFormatter.Normalize(Version)}"
                .ToLowerInvariant();
        }
    }

    public class PackageAuditRecord : AuditRecord<AuditedPackageAction>
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }

        public AuditedPackage PackageRecord { get; set; }
        public AuditedPackageRegistration RegistrationRecord { get; set; }

        public string Reason { get; set; }

        public PackageAuditRecord()
        {

        }
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

        public PackageAuditRecord(string id, string version, AuditedPackageAction action, string reason)
            : this(id,
                  version,
                  hash: "",
                  packageRecord: null,
                  registrationRecord: null,
                  action: action,
                  reason: reason)
        { }

        public PackageAuditRecord(Package package, AuditedPackageAction action, string reason)
            : this(package.PackageRegistration.Id,
                  package.Version,
                  package.Hash,
                  packageRecord: null,
                  registrationRecord: null,
                  action: action,
                  reason: reason)
        {
            PackageRecord = AuditedPackage.CreateFrom(package);
            RegistrationRecord = AuditedPackageRegistration.CreateFrom(package.PackageRegistration);
        }

        public PackageAuditRecord(Package package, AuditedPackageAction action)
            : this(package.PackageRegistration.Id,
                  package.Version,
                  package.Hash,
                  packageRecord: null,
                  registrationRecord: null,
                  action: action,
                  reason: null)
        {
            PackageRecord = AuditedPackage.CreateFrom(package);
            RegistrationRecord = AuditedPackageRegistration.CreateFrom(package.PackageRegistration);
        }

        public override string GetPath()
        {
            return $"{Id}/{NuGetVersionFormatter.Normalize(Version)}"
                .ToLowerInvariant();
        }
    }
}
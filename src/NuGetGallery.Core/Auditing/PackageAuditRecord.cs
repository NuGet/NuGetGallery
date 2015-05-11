// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NuGetGallery.Auditing
{
    public class PackageAuditRecord : AuditRecord<PackageAuditAction>
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }

        public DataTable PackageRecord { get; set; }
        public DataTable RegistrationRecord { get; set; }

        public string Reason { get; set; }

        public PackageAuditRecord(string id, string version, string hash, DataTable packageRecord, DataTable registrationRecord, PackageAuditAction action, string reason)
            : base(action)
        {
            Id = id;
            Version = version;
            Hash = hash;
            PackageRecord = packageRecord;
            RegistrationRecord = registrationRecord;
            Reason = reason;
        }

        public PackageAuditRecord(Package package, DataTable packageRecord, DataTable registrationRecord, PackageAuditAction action, string reason)
            : this(package.PackageRegistration.Id, package.Version, package.Hash, packageRecord, registrationRecord, action, reason)
        {
        }

        public override string GetPath()
        {
            return String.Format(
                "{0}/{1}",
                Id.ToLowerInvariant(), 
                SemanticVersionExtensions.Normalize(Version).ToLowerInvariant());
        }
    }

    public enum PackageAuditAction
    {
        Deleted
    }
}

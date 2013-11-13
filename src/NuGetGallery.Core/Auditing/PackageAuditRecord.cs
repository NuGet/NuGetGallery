using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NuGetGallery.Auditing
{
    public class PackageAuditRecord : AuditRecord
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Hash { get; set; }

        public DataTable PackageRecord { get; set; }
        public DataTable RegistrationRecord { get; set; }

        public PackageAuditAction Action { get; set; }

        public string Reason { get; set; }
        
        public PackageAuditRecord(Package package, DataTable packageRecord, DataTable registrationRecord, PackageAuditAction action, string reason)
        {
            Id = package.PackageRegistration.Id;
            Version = package.Version;
            Hash = package.Hash;
            PackageRecord = packageRecord;
            RegistrationRecord = registrationRecord;
            Action = action;
            Reason = reason;
        }

        public override string GetPath()
        {
            return String.Format(
                "{0}/{1}/{2}-{3}.json",
                Id.ToLowerInvariant(), 
                SemanticVersionExtensions.Normalize(Version).ToLowerInvariant(), 
                DateTime.UtcNow.ToString("s"), // Sortable DateTime Format
                Action.ToString().ToLowerInvariant());
        }
    }

    public enum PackageAuditAction
    {
        Deleted
    }
}

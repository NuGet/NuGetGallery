using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NuGetGallery.Operations.Model
{
    public class PackageAuditRecord : AuditRecord
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Hash { get; set; }

        public DataTable PackageRecord { get; set; }
        public DataTable RegistrationRecord { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public PackageAuditAction Action { get; set; }

        public string Reason { get; set; }
        
        public PackageAuditRecord(Package package, DataTable packageRecord, DataTable registrationRecord, PackageAuditAction action, string reason, AuditEnvironment environment) : base(environment)
        {
            Id = package.Id;
            Version = package.Version;
            Hash = package.Hash;
            PackageRecord = packageRecord;
            RegistrationRecord = registrationRecord;
            Action = action;
            Reason = reason;
        }
    }
    public enum PackageAuditAction
    {
        Deleted
    }
}

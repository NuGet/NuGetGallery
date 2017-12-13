// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class PackageAuditRecordObfuscator : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PackageAuditRecord);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            PackageAuditRecord record = (PackageAuditRecord)value;
            JToken t = JToken.FromObject(Obfuscate(record));
            t.WriteTo(writer);
        }

        private PackageAuditRecord Obfuscate(PackageAuditRecord record)
        {
            var obfuscatedAuditedPackage = AuditedPackage.CreateObfuscatedAuditPackage(record.PackageRecord);
            return new PackageAuditRecord(record.Id, record.Version, record.Hash, obfuscatedAuditedPackage, record.RegistrationRecord, record.Action, record.Reason);
        }
    }
}

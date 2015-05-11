// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NuGetGallery.Auditing
{
    public abstract class AuditingService
    {
        public static readonly AuditingService None = new NullAuditingService();

        private static readonly JsonSerializerSettings _auditRecordSerializerSettings;

        static AuditingService()
        {
            var settings = new JsonSerializerSettings()
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DefaultValueHandling = DefaultValueHandling.Include,
                Formatting = Formatting.Indented,
                MaxDepth = 10,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                TypeNameHandling = TypeNameHandling.None
            };
            settings.Converters.Add(new StringEnumConverter());
            _auditRecordSerializerSettings = settings;
        }

        public virtual async Task<Uri> SaveAuditRecord(AuditRecord record)
        {
            // Build an audit entry
            var entry = new AuditEntry(record, await GetActor());

            // Serialize to json
            string rendered = RenderAuditEntry(entry);

            // Save the record
            return await SaveAuditRecord(rendered, record.GetResourceType(), record.GetPath(), record.GetAction(), entry.Actor.TimestampUtc);
        }

        public virtual string RenderAuditEntry(AuditEntry entry)
        {
            return JsonConvert.SerializeObject(entry, _auditRecordSerializerSettings);
        }

        /// <summary>
        /// Performs the actual saving of audit data to an audit store
        /// </summary>
        /// <param name="auditData">The data to store in the audit record</param>
        /// <param name="resourceType">The type of resource affected by the audit (usually used as the first-level folder)</param>
        /// <param name="filePath">The file-system path to use to identify the audit record</param>
        /// <param name="action">The action recorded in this audit record</param>
        /// <param name="timestamp">A timestamp indicating when the record was created</param>
        /// <returns>The URI identifying the audit record resource</returns>
        protected abstract Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp);

        protected virtual Task<AuditActor> GetActor()
        {
            return AuditActor.GetCurrentMachineActor();
        }

        private class NullAuditingService : AuditingService
        {
            protected override Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
            {
                return Task.FromResult<Uri>(new Uri("http://auditing.local/" + resourceType + "/" + filePath + "/" + timestamp.ToString("s") + "-" + action.ToLowerInvariant()));
            }
        }
    }
}

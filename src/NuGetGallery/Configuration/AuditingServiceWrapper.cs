using NuGetGallery.Auditing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;

namespace NuGetGallery.Configuration
{
    public class CloudAuditingServiceWrapper : AuditingService
    {
        private Task<CloudAuditingService> _auditingServiceTask;

        public CloudAuditingServiceWrapper(Task<CloudAuditingService> auditingServiceTask)
        {
            _auditingServiceTask = auditingServiceTask;
        }

        public override async Task<Uri> SaveAuditRecord(AuditRecord record)
        {
            return await (await _auditingServiceTask).SaveAuditRecord(record);
        }

        protected override Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
        {
            throw new NotImplementedException();
        }
    }
}
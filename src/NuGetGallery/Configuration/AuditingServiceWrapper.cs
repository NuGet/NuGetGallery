// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing;
using System;
using System.Threading.Tasks;

namespace NuGetGallery.Configuration
{
    /// <summary>
    /// Wrapper class to call SaveAuditRecord on an AuditingService returned by a task.
    /// </summary>
    public class CloudAuditingServiceWrapper : AuditingService
    {
        private Task<CloudAuditingService> _auditingServiceTask;

        /// <summary>
        /// Initializes a wrapper that calls SaveAuditRecord on an AuditingService returned by a task.
        /// </summary>
        /// <param name="auditingServiceTask">Task returning an AuditingService to call SaveAuditRecord on.</param>
        public CloudAuditingServiceWrapper(Task<CloudAuditingService> auditingServiceTask)
        {
            _auditingServiceTask = auditingServiceTask;
        }

        public override async Task<Uri> SaveAuditRecord(AuditRecord record)
        {
            // Override the method in AuditingService to call SaveAuditRecord on _auditingServiceTask instead of this class.
            return await (await _auditingServiceTask).SaveAuditRecord(record);
        }

        protected override Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
        {
            // This method should never be called because we have overriden the method that calls it.
            throw new NotImplementedException();
        }
    }
}
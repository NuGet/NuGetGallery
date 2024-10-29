// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery.Auditing;

namespace GitHubVulnerabilities2Db.Gallery
{
    /// <remarks>
    /// This job should not attempt to audit.
    /// </remarks>
    public class ThrowingAuditingService : IAuditingService
    {
        public Task SaveAuditRecordAsync(AuditRecord record)
        {
            throw new System.NotImplementedException();
        }
    }
}
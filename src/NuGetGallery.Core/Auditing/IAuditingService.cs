// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// Base interface for an auditing service.
    /// </summary>
    public interface IAuditingService
    {
        /// <summary>
        /// Persists the audit record to storage.
        /// </summary>
        /// <param name="record">An audit record.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous save operation.</returns> 
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="record" /> is <c>null</c>.</exception>
        Task SaveAuditRecordAsync(AuditRecord record);
    }
}
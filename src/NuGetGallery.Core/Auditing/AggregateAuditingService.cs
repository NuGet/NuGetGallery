// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// An auditing service that aggregates multiple auditing services.
    /// </summary>
    public sealed class AggregateAuditingService : IAuditingService
    {
        private readonly IAuditingService[] _services;

        /// <summary>
        /// Instantiates a new instance.
        /// </summary>
        /// <param name="services">An enumerable of <see cref="IAuditingService" /> instances.</param>
        /// <exception cref="ArgumentNullException">Thrown if <see cref="services" /> is <c>null</c>.</exception>
        public AggregateAuditingService(IEnumerable<IAuditingService> services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            _services = services.ToArray();
        }

        /// <summary>
        /// Persists the audit record to storage.
        /// </summary>
        /// <param name="record">An audit record.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous save operation.</returns> 
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="record" /> is <c>null</c>.</exception>
        public async Task SaveAuditRecordAsync(AuditRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            var tasks = _services.Select(service => service.SaveAuditRecordAsync(record));
            await Task.WhenAll(tasks);
        }
    }
}
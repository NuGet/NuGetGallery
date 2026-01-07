// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;

namespace NuGet.Services.Revalidate
{
    public class PackageRevalidationStateService : IPackageRevalidationStateService
    {
        private readonly IValidationEntitiesContext _context;
        private readonly IPackageRevalidationInserter _inserter;
        private readonly ILogger<PackageRevalidationStateService> _logger;

        public PackageRevalidationStateService(
            IValidationEntitiesContext context,
            IPackageRevalidationInserter inserter,
            ILogger<PackageRevalidationStateService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _inserter = inserter ?? throw new ArgumentNullException(nameof(inserter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task AddPackageRevalidationsAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
            return _inserter.AddPackageRevalidationsAsync(revalidations);
        }

        public async Task<int> RemovePackageRevalidationsAsync(int max)
        {
            var revalidations = await _context.PackageRevalidations
                .Take(max)
                .ToListAsync();

            if (revalidations.Any())
            {
                foreach (var revalidation in revalidations)
                {
                    _context.PackageRevalidations.Remove(revalidation);
                }

                await _context.SaveChangesAsync();
            }

            return revalidations.Count;
        }

        public async Task<int> PackageRevalidationCountAsync()
        {
            return await _context.PackageRevalidations.CountAsync();
        }

        public async Task<int> CountRevalidationsEnqueuedInPastHourAsync()
        {
            var lowerBound = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));

            return await _context.PackageRevalidations
                .Where(r => r.Enqueued >= lowerBound)
                .CountAsync();
        }

        public async Task MarkPackageRevalidationsAsEnqueuedAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
            try
            {
                var enqueueTime = DateTime.UtcNow;
                foreach (var revalidation in revalidations)
                {
                    revalidation.Enqueued = enqueueTime;
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Failed to update revalidations as enqueued");
                throw;
            }
        }
    }
}

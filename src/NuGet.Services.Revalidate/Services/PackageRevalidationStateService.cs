// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private readonly ILogger<PackageRevalidationStateService> _logger;

        public PackageRevalidationStateService(IValidationEntitiesContext context, ILogger<PackageRevalidationStateService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task AddPackageRevalidationsAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
            _logger.LogDebug("Persisting package revalidations to database...");

            var validationContext = _context as ValidationEntitiesContext;

            if (validationContext != null)
            {
                validationContext.Configuration.AutoDetectChangesEnabled = false;
                validationContext.Configuration.ValidateOnSaveEnabled = false;
            }

            foreach (var revalidation in revalidations)
            {
                _context.PackageRevalidations.Add(revalidation);
            }

            _logger.LogDebug("Saving the validation context...");

            await _context.SaveChangesAsync();

            _logger.LogDebug("Finished saving the validation context...");

            if (validationContext != null)
            {
                validationContext.Configuration.AutoDetectChangesEnabled = true;
                validationContext.Configuration.ValidateOnSaveEnabled = true;
            }

            _logger.LogDebug("Finished persisting package revalidations to database...");
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

        public async Task MarkPackageRevalidationAsEnqueuedAsync(PackageRevalidation revalidation)
        {
            try
            {
                revalidation.Enqueued = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning(
                    "Failed to update revalidation as enqueued for {PackageId} {PackageNormalizedVersion}",
                    revalidation.PackageId,
                    revalidation.PackageNormalizedVersion);

                throw;
            }
        }
    }
}

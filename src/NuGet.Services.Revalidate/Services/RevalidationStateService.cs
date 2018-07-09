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
    public class RevalidationStateService : IRevalidationStateService
    {
        private readonly IValidationEntitiesContext _context;
        private readonly ILogger<RevalidationStateService> _logger;

        public RevalidationStateService(IValidationEntitiesContext context, ILogger<RevalidationStateService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> IsKillswitchActiveAsync()
        {
            // TODO
            return Task.FromResult(false);
        }

        public async Task AddPackageRevalidationsAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
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

            await _context.SaveChangesAsync();

            if (validationContext != null)
            {
                validationContext.Configuration.AutoDetectChangesEnabled = true;
                validationContext.Configuration.ValidateOnSaveEnabled = true;
            }
        }

        public async Task<int> RemoveRevalidationsAsync(int max)
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

        public async Task MarkRevalidationAsEnqueuedAsync(PackageRevalidation revalidation)
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

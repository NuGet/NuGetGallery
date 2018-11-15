// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    using IGalleryContext = IEntitiesContext;

    public class RevalidationQueue : IRevalidationQueue
    {
        private readonly IGalleryContext _galleryContext;
        private readonly IValidationEntitiesContext _validationContext;
        private readonly RevalidationQueueConfiguration _config;
        private readonly ITelemetryService _telemetry;
        private readonly ILogger<RevalidationQueue> _logger;

        public RevalidationQueue(
            IGalleryContext galleryContext,
            IValidationEntitiesContext validationContext,
            RevalidationQueueConfiguration config,
            ITelemetryService telemetry,
            ILogger<RevalidationQueue> logger)
        {
            _galleryContext = galleryContext ?? throw new ArgumentNullException(nameof(galleryContext));
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<PackageRevalidation>> NextAsync()
        {
            // Find the next package to revalidate. We will skip packages if:
            //   1. The package has more than "MaximumPackageVersions" versions
            //   2. The package has already been enqueued for revalidation
            //   3. The package's revalidation was completed by an external factory (like manual admin revalidation)
            List<PackageRevalidation> next;
            using (_telemetry.TrackFindNextRevalidations())
            {
                _logger.LogInformation("Finding the next packages to revalidate...");

                IQueryable<PackageRevalidation> query = _validationContext.PackageRevalidations;

                if (_config.MaximumPackageVersions.HasValue)
                {
                    query = query.Where(
                        r =>
                        !_validationContext.PackageRevalidations.GroupBy(r2 => r2.PackageId)
                        .Where(g => g.Count() > _config.MaximumPackageVersions)
                        .Any(g => g.Key == r.PackageId));
                }

                next = await query
                    .Where(r => r.Enqueued == null)
                    .Where(r => r.Completed == false)
                    .OrderBy(r => r.Key)
                    .Take(_config.MaxBatchSize)
                    .ToListAsync();
            }

            _logger.LogInformation("Found {Revalidations} packages to revalidate", next.Count);

            // Return all the revalidations that aren't already completed.
            return await FilterCompletedRevalidationsAsync(next);
        }

        private async Task<IReadOnlyList<PackageRevalidation>> FilterCompletedRevalidationsAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
            if (!revalidations.Any())
            {
                return revalidations;
            }

            var completed = new List<PackageRevalidation>();
            var uncompleted = revalidations.ToDictionary(
                r => $"{r.PackageId}/{r.PackageNormalizedVersion}",
                r => r);

            // Packages that already have a repository signature do not need to be revalidated.
            _logger.LogInformation("Finding revalidations that can be skipped because their packages are already repository signed...");

            var hasRepositorySignatures = await _validationContext.PackageSigningStates
                .Select(s => new {
                    IdAndVersion = s.PackageId + "/" + s.PackageNormalizedVersion,
                    s.PackageSignatures
                })
                .Where(s => uncompleted.Keys.Contains(s.IdAndVersion))
                .Where(s => s.PackageSignatures.Any(sig => sig.Type == PackageSignatureType.Repository))
                .Select(s => s.IdAndVersion)
                .ToListAsync();

            _logger.LogInformation(
                "Found {RevalidationCount} revalidations that can be skipped because their packages are already repository signed",
                hasRepositorySignatures.Count);

            foreach (var idAndVersion in hasRepositorySignatures)
            {
                completed.Add(uncompleted[idAndVersion]);
                uncompleted.Remove(idAndVersion);
            }

            // Packages that are no longer available should not be revalidated.
            _logger.LogInformation("Finding revalidations' package statuses...");

            var packageStatuses = await _galleryContext.Set<Package>()
                .Select(p => new
                {
                    Identity = p.PackageRegistration.Id + "/" + p.NormalizedVersion,
                    p.PackageStatusKey
                })
                .Where(p => uncompleted.Keys.Contains(p.Identity))
                .ToDictionaryAsync(
                    p => p.Identity,
                    p => p.PackageStatusKey);

            _logger.LogInformation("Found {PackageStatusCount} revalidations' package statuses", packageStatuses.Count);

            foreach (var key in uncompleted.Keys.ToList())
            {
                // Packages that are hard deleted won't have a status.
                if (!packageStatuses.TryGetValue(key, out var status) || status == PackageStatus.Deleted)
                {
                    completed.Add(uncompleted[key]);
                    uncompleted.Remove(key);
                    continue;
                }
            }

            _logger.LogInformation(
                "Found {CompletedRevalidations} revalidations that can be skipped. There are {UncompletedRevalidations} " +
                "revalidations remaining in this batch",
                completed.Count,
                uncompleted.Count);

            // Update revalidations that were determined to be completed and return the remaining revalidations.
            if (completed.Any())
            {
                await MarkRevalidationsAsCompletedAsync(completed);
            }

            return uncompleted.Values.ToList();
        }

        private async Task MarkRevalidationsAsCompletedAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
            try
            {
                foreach (var revalidation in revalidations)
                {
                    _logger.LogInformation(
                        "Marking package {PackageId} {PackageNormalizedVersion} revalidation as completed as the package is unavailable or the package is already repository signed...",
                        revalidation.PackageId,
                        revalidation.PackageNormalizedVersion);

                    revalidation.Completed = true;
                }

                await _validationContext.SaveChangesAsync();

                foreach (var revalidation in revalidations)
                {
                    _logger.LogInformation(
                        "Marked package {PackageId} {PackageNormalizedVersion} revalidation as completed",
                        revalidation.PackageId,
                        revalidation.PackageNormalizedVersion);

                    _telemetry.TrackPackageRevalidationMarkedAsCompleted(revalidation.PackageId, revalidation.PackageNormalizedVersion);
                }
            }
            catch (DbUpdateConcurrencyException e)
            {
                _logger.LogError(
                    0,
                    e,
                    "Failed to mark package revalidations as completed. " +
                    $"These revalidations will be marked as completed on the next iteration of {nameof(NextAsync)}...");
            }
        }
    }
}

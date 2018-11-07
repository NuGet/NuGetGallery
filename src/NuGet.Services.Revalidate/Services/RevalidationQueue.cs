// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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

        public async Task<PackageRevalidation> NextOrNullAsync()
        {
            for (var i = 0; i < _config.MaximumAttempts; i++)
            {
                _logger.LogInformation(
                    "Attempting to find the next revalidation. Try {Attempt} of {MaxAttempts}",
                    i + 1,
                    _config.MaximumAttempts);

                // Find the next package to revalidate. We will skip packages if:
                //   1. The package has more than "MaximumPackageVersions" versions
                //   2. The package has already been enqueued for revalidation
                //   3. The package's revalidation was completed by an external factory (like manual admin revalidation)
                IQueryable<PackageRevalidation> query = _validationContext.PackageRevalidations;

                if (_config.MaximumPackageVersions.HasValue)
                {
                    query = query.Where(
                        r =>
                        !_validationContext.PackageRevalidations.GroupBy(r2 => r2.PackageId)
                        .Where(g => g.Count() > _config.MaximumPackageVersions)
                        .Any(g => g.Key == r.PackageId));
                }

                var next = await query
                    .Where(r => r.Enqueued == null)
                    .Where(r => r.Completed == false)
                    .OrderBy(r => r.Key)
                    .FirstOrDefaultAsync();

                if (next == null)
                {
                    _logger.LogWarning("Could not find any incomplete revalidations");
                    return null;
                }

                // Don't revalidate packages that already have a repository signature or that no longer exist.
                if (await HasRepositorySignature(next) || await IsDeleted(next))
                {
                    await MarkAsCompleted(next);
                    await Task.Delay(_config.SleepBetweenAttempts);

                    continue;
                }

                _logger.LogInformation(
                    "Found revalidation for {PackageId} {PackageNormalizedVersion} after {Attempt} attempts",
                    next.PackageId,
                    next.PackageNormalizedVersion,
                    i + 1);

                return next;
            }

            _logger.LogInformation(
                "Did not find any revalidations after {MaxAttempts}. Retry later...",
                _config.MaximumAttempts);

            return null;
        }

        private Task<bool> HasRepositorySignature(PackageRevalidation revalidation)
        {
            return _validationContext.PackageSigningStates
                .Where(s => s.PackageId == revalidation.PackageId)
                .Where(s => s.PackageNormalizedVersion == revalidation.PackageNormalizedVersion)
                .Where(s => s.PackageSignatures.Any(sig => sig.Type == PackageSignatureType.Repository))
                .AnyAsync();
        }

        private async Task<bool> IsDeleted(PackageRevalidation revalidation)
        {
            var packageStatus = await _galleryContext.Set<Package>()
                .Where(p => p.PackageRegistration.Id == revalidation.PackageId)
                .Where(p => p.NormalizedVersion == revalidation.PackageNormalizedVersion)
                .Select(p => (PackageStatus?)p.PackageStatusKey)
                .FirstOrDefaultAsync();

            return (packageStatus == null || packageStatus == PackageStatus.Deleted);
        }

        private async Task MarkAsCompleted(PackageRevalidation revalidation)
        {
            _logger.LogInformation(
                "Marking package revalidation as completed as it has a repository signature or is deleted for {PackageId} {PackageNormalizedVersion}",
                revalidation.PackageId,
                revalidation.PackageNormalizedVersion);

            try
            {
                revalidation.Completed = true;

                await _validationContext.SaveChangesAsync();

                _telemetry.TrackPackageRevalidationMarkedAsCompleted(revalidation.PackageId, revalidation.PackageNormalizedVersion);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Swallow concurrency exceptions. The package will be marked as completed
                // on the next iteration of "NextOrNullAsync".
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace Gallery.Maintenance
{
    /// <summary>
    /// Deletes expired private staging through the shared deletion and blob-cleanup paths.
    /// </summary>
    public class ExpireStagingTask : MaintenanceTask
    {
        private const int BatchSize = 100;

        public ExpireStagingTask(ILogger<ExpireStagingTask> logger) : base(logger)
        {
        }

        public override async Task RunAsync(Job job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (!job.GetStagingExpirationConfiguration().Enabled)
            {
                _logger.LogInformation("Automatic staging expiration is disabled.");
                return;
            }

            using (var entities = new EntitiesContext(await job.CreateSqlConnectionAsync<NuGet.Jobs.Configuration.GalleryDbConfiguration>(), readOnly: false))
            {
                var packages = new EntityRepository<StagedPackage>(entities);
                var symbols = new EntityRepository<StagedSymbolPackage>(entities);
                var groups = new EntityRepository<StagingGroup>(entities);
                var packageService = new CorePackageService(
                    new EntityRepository<Package>(entities), new EntityRepository<PackageRegistration>(entities), new EntityRepository<Certificate>(entities));
                var deletion = new StagingDeletionService(
                    packages, symbols, new EntityRepository<StagedPackageIdentity>(entities),
                    new EntityRepository<SymbolPackage>(entities), groups, packageService, new StagingBlobCleanupService(entities));
                await ProcessAsync(groups, packages, symbols, deletion);
            }
        }

        /// <summary>
        /// Expires groups and standalone artifacts in bounded pages, protecting accepted promotions.
        /// </summary>
        public async Task ProcessAsync(
            IEntityRepository<StagingGroup> groups,
            IEntityRepository<StagedPackage> packages,
            IEntityRepository<StagedSymbolPackage> symbols,
            StagingDeletionService deletion)
        {
            if (groups == null)
            {
                throw new ArgumentNullException(nameof(groups));
            }

            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            if (symbols == null)
            {
                throw new ArgumentNullException(nameof(symbols));
            }

            if (deletion == null)
            {
                throw new ArgumentNullException(nameof(deletion));
            }

            var cutoff = DateTime.UtcNow;
            var lastKey = 0;
            while (true)
            {
                var expired = await groups.GetAll()
                    .Where(group => group.Key > lastKey && group.ExpirationDate <= cutoff && !group.ActivePromotionId.HasValue)
                    .OrderBy(group => group.Key)
                    .Take(BatchSize)
                    .ToListAsync();
                if (expired.Count == 0)
                {
                    break;
                }

                foreach (var group in expired)
                {
                    lastKey = group.Key;
                    await groups.ExecuteInTransactionAsync(async () =>
                    {
                        if (group.ExpirationDate > cutoff || group.ActivePromotionId.HasValue)
                        {
                            _logger.LogInformation("Skipping refreshed or promoting staging group {GroupKey}.", group.Key);
                            return;
                        }

                        var result = await deletion.DeleteGroupAsync(group);
                        _logger.LogInformation("Expiration of staging group {GroupKey}: {Result}, {ArtifactCount} artifacts.", group.Key, result.Type, result.AffectedPackageCount);
                    });
                }
            }

            lastKey = 0;
            while (true)
            {
                var expired = await packages.GetAll()
                    .Include(attempt => attempt.StagedPackageIdentity.Package.PackageRegistration)
                    .Include(attempt => attempt.StagedPackageIdentity.CurrentStagedSymbolPackage.SymbolPackage)
                    .Where(attempt => attempt.Key > lastKey && attempt.ExpirationDate <= cutoff)
                    .Where(attempt => !attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
                    .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedPackageKey == attempt.Key)
                    .Where(attempt => attempt.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged)
                    .Where(attempt => attempt.Status != StagedPackageStatus.Deleted && attempt.Status != StagedPackageStatus.Superseded && attempt.Status != StagedPackageStatus.Promoting)
                    .OrderBy(attempt => attempt.Key)
                    .Take(BatchSize)
                    .ToListAsync();
                if (expired.Count == 0)
                {
                    break;
                }

                foreach (var attempt in expired)
                {
                    lastKey = attempt.Key;
                    await packages.ExecuteInTransactionAsync(async () =>
                    {
                        var identity = attempt.StagedPackageIdentity;
                        if (attempt.ExpirationDate > cutoff || identity.StagingGroupKey.HasValue || identity.CurrentStagedPackageKey != attempt.Key)
                        {
                            _logger.LogInformation("Skipping changed staged package {AttemptKey}.", attempt.Key);
                            return;
                        }

                        if (identity.Package.PackageStatusKey != PackageStatus.Staged)
                        {
                            _logger.LogInformation("Skipping published or deleted staged package {AttemptKey}.", attempt.Key);
                            return;
                        }

                        if (attempt.Status == StagedPackageStatus.Promoting || identity.CurrentStagedSymbolPackage?.Status == StagedPackageStatus.Promoting)
                        {
                            _logger.LogInformation("Skipping promoting staged package {AttemptKey}.", attempt.Key);
                            return;
                        }

                        await deletion.DeletePackageAsync(attempt);
                        await packages.CommitChangesAsync();
                        _logger.LogInformation("Expired staged package {AttemptKey}.", attempt.Key);
                    });
                }
            }

            lastKey = 0;
            while (true)
            {
                var expired = await symbols.GetAll()
                    .Include(attempt => attempt.SymbolPackage)
                    .Include(attempt => attempt.StagedPackageIdentity.CurrentStagedPackage)
                    .Where(attempt => attempt.Key > lastKey && attempt.ExpirationDate <= cutoff)
                    .Where(attempt => !attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
                    .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key)
                    .Where(attempt => attempt.SymbolPackage.StatusKey == PackageStatus.Staged)
                    .Where(attempt => attempt.Status != StagedPackageStatus.Promoting)
                    .OrderBy(attempt => attempt.Key)
                    .Take(BatchSize)
                    .ToListAsync();
                if (expired.Count == 0)
                {
                    return;
                }

                foreach (var attempt in expired)
                {
                    lastKey = attempt.Key;
                    await symbols.ExecuteInTransactionAsync(async () =>
                    {
                        var identity = attempt.StagedPackageIdentity;
                        if (attempt.ExpirationDate > cutoff || identity.StagingGroupKey.HasValue || identity.CurrentStagedSymbolPackageKey != attempt.Key)
                        {
                            _logger.LogInformation("Skipping changed staged symbols {AttemptKey}.", attempt.Key);
                            return;
                        }

                        if (attempt.SymbolPackage.StatusKey != PackageStatus.Staged)
                        {
                            _logger.LogInformation("Skipping published or deleted staged symbols {AttemptKey}.", attempt.Key);
                            return;
                        }

                        if (attempt.Status == StagedPackageStatus.Promoting || identity.CurrentStagedPackage?.Status == StagedPackageStatus.Promoting)
                        {
                            _logger.LogInformation("Skipping promoting staged symbols {AttemptKey}.", attempt.Key);
                            return;
                        }

                        await deletion.DeleteSymbolPackageAsync(attempt);
                        _logger.LogInformation("Expired staged symbols {AttemptKey}.", attempt.Key);
                    });
                }
            }
        }
    }
}

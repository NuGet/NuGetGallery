// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

            await ProcessAsync(async () =>
                new EntitiesContext(await job.CreateSqlConnectionAsync<NuGet.Jobs.Configuration.GalleryDbConfiguration>(), readOnly: false));
        }

        /// <summary>
        /// Reads bounded key pages and expires each candidate in its own disposable context.
        /// </summary>
        public async Task ProcessAsync(Func<Task<IEntitiesContext>> createContext)
        {
            if (createContext == null)
            {
                throw new ArgumentNullException(nameof(createContext));
            }

            var cutoff = DateTime.UtcNow;
            await ProcessPagesAsync(
                createContext,
                (entities, lastKey) => entities.StagingGroups.AsNoTracking()
                    .Where(group => group.Key > lastKey && group.ExpirationDate <= cutoff && !group.ActivePromotionId.HasValue)
                    .Select(group => group.Key),
                (entities, key) => ExpireGroupAsync(entities, key, cutoff));
            await ProcessPagesAsync(
                createContext,
                (entities, lastKey) => entities.StagedPackages.AsNoTracking()
                    .Where(attempt => attempt.Key > lastKey && attempt.ExpirationDate <= cutoff)
                    .Where(attempt => !attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
                    .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedPackageKey == attempt.Key)
                    .Where(attempt => attempt.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged)
                    .Where(attempt => attempt.Status != StagedPackageStatus.Deleted && attempt.Status != StagedPackageStatus.Superseded && attempt.Status != StagedPackageStatus.Promoting)
                    .Select(attempt => attempt.Key),
                (entities, key) => ExpirePackageAsync(entities, key, cutoff));
            await ProcessPagesAsync(
                createContext,
                (entities, lastKey) => entities.StagedSymbolPackages.AsNoTracking()
                    .Where(attempt => attempt.Key > lastKey && attempt.ExpirationDate <= cutoff)
                    .Where(attempt => !attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
                    .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key)
                    .Where(attempt => attempt.SymbolPackage.StatusKey == PackageStatus.Staged)
                    .Where(attempt => attempt.Status != StagedPackageStatus.Promoting)
                    .Select(attempt => attempt.Key),
                (entities, key) => ExpireSymbolsAsync(entities, key, cutoff));
        }

        private static async Task ProcessPagesAsync(
            Func<Task<IEntitiesContext>> createContext,
            Func<IEntitiesContext, int, IQueryable<int>> getKeys,
            Func<IEntitiesContext, int, Task> expire)
        {
            var lastKey = 0;
            while (true)
            {
                List<int> keys;
                using (var entities = await createContext())
                {
                    keys = await getKeys(entities, lastKey).OrderBy(key => key).Take(BatchSize).ToListAsync();
                }

                if (keys.Count == 0)
                {
                    return;
                }

                foreach (var key in keys)
                {
                    lastKey = key;
                    using (var entities = await createContext())
                    {
                        await expire(entities, key);
                    }
                }
            }
        }

        private async Task ExpireGroupAsync(IEntitiesContext entities, int key, DateTime cutoff)
        {
            var groups = new EntityRepository<StagingGroup>(entities);
            await groups.ExecuteInTransactionAsync(async () =>
            {
                var group = await groups.GetAll().SingleOrDefaultAsync(candidate => candidate.Key == key);
                if (group == null || group.ExpirationDate > cutoff || group.ActivePromotionId.HasValue)
                {
                    _logger.LogInformation("Skipping removed, refreshed or promoting staging group {GroupKey}.", key);
                    return;
                }

                var result = await CreateDeletionService(entities).DeleteGroupAsync(group);
                _logger.LogInformation("Expiration of staging group {GroupKey}: {Result}, {ArtifactCount} artifacts.", key, result.Type, result.AffectedPackageCount);
            });
        }

        private async Task ExpirePackageAsync(IEntitiesContext entities, int key, DateTime cutoff)
        {
            var packages = new EntityRepository<StagedPackage>(entities);
            await packages.ExecuteInTransactionAsync(async () =>
            {
                var attempt = await packages.GetAll()
                    .Include(candidate => candidate.StagedPackageIdentity.Package.PackageRegistration)
                    .Include(candidate => candidate.StagedPackageIdentity.CurrentStagedSymbolPackage.SymbolPackage)
                    .SingleOrDefaultAsync(candidate => candidate.Key == key);
                if (attempt == null)
                {
                    _logger.LogInformation("Skipping removed staged package {AttemptKey}.", key);
                    return;
                }

                var identity = attempt.StagedPackageIdentity;
                if (attempt.ExpirationDate > cutoff || identity.StagingGroupKey.HasValue || identity.CurrentStagedPackageKey != key)
                {
                    _logger.LogInformation("Skipping changed staged package {AttemptKey}.", key);
                    return;
                }

                if (identity.Package.PackageStatusKey != PackageStatus.Staged || attempt.Status == StagedPackageStatus.Deleted || attempt.Status == StagedPackageStatus.Superseded)
                {
                    _logger.LogInformation("Skipping published or retired staged package {AttemptKey}.", key);
                    return;
                }

                if (attempt.Status == StagedPackageStatus.Promoting || identity.CurrentStagedSymbolPackage?.Status == StagedPackageStatus.Promoting)
                {
                    _logger.LogInformation("Skipping promoting staged package {AttemptKey}.", key);
                    return;
                }

                await CreateDeletionService(entities).DeletePackageAsync(attempt);
                await packages.CommitChangesAsync();
                _logger.LogInformation("Expired staged package {AttemptKey}.", key);
            });
        }

        private async Task ExpireSymbolsAsync(IEntitiesContext entities, int key, DateTime cutoff)
        {
            var symbols = new EntityRepository<StagedSymbolPackage>(entities);
            await symbols.ExecuteInTransactionAsync(async () =>
            {
                var attempt = await symbols.GetAll()
                    .Include(candidate => candidate.SymbolPackage)
                    .Include(candidate => candidate.StagedPackageIdentity.CurrentStagedPackage)
                    .SingleOrDefaultAsync(candidate => candidate.Key == key);
                if (attempt == null)
                {
                    _logger.LogInformation("Skipping removed staged symbols {AttemptKey}.", key);
                    return;
                }

                var identity = attempt.StagedPackageIdentity;
                if (attempt.ExpirationDate > cutoff || identity.StagingGroupKey.HasValue || identity.CurrentStagedSymbolPackageKey != key)
                {
                    _logger.LogInformation("Skipping changed staged symbols {AttemptKey}.", key);
                    return;
                }

                if (attempt.SymbolPackage.StatusKey != PackageStatus.Staged)
                {
                    _logger.LogInformation("Skipping published or deleted staged symbols {AttemptKey}.", key);
                    return;
                }

                if (attempt.Status == StagedPackageStatus.Promoting || identity.CurrentStagedPackage?.Status == StagedPackageStatus.Promoting)
                {
                    _logger.LogInformation("Skipping promoting staged symbols {AttemptKey}.", key);
                    return;
                }

                await CreateDeletionService(entities).DeleteSymbolPackageAsync(attempt);
                _logger.LogInformation("Expired staged symbols {AttemptKey}.", key);
            });
        }

        private static StagingDeletionService CreateDeletionService(IEntitiesContext entities)
        {
            var packageService = new CorePackageService(
                new EntityRepository<Package>(entities), new EntityRepository<PackageRegistration>(entities), new EntityRepository<Certificate>(entities));
            return new StagingDeletionService(
                new EntityRepository<StagedPackage>(entities), new EntityRepository<StagedSymbolPackage>(entities),
                new EntityRepository<StagedPackageIdentity>(entities), new EntityRepository<SymbolPackage>(entities),
                new EntityRepository<StagingGroup>(entities), packageService, new StagingBlobCleanupService(entities));
        }
    }
}

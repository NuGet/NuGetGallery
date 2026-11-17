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
using NuGet.Services.Messaging.Email;
using NuGetGallery;
using NuGetGallery.Infrastructure.Mail.Messages;

namespace Gallery.Maintenance
{
    /// <summary>
    /// Warns owners about expiration and deletes expired private staging through shared cleanup paths.
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
                new EntitiesContext(await job.CreateSqlConnectionAsync<NuGet.Jobs.Configuration.GalleryDbConfiguration>(), readOnly: false),
                job.GetMessageService(), job.GetEmailConfiguration(), DateTime.UtcNow);
        }

        /// <summary>
        /// Reads bounded key pages and warns or expires each candidate in its own disposable context.
        /// </summary>
        public async Task ProcessAsync(Func<Task<IEntitiesContext>> createContext, IMessageService messages, MaintenanceEmailConfiguration email, DateTime cutoff)
        {
            if (createContext == null)
            {
                throw new ArgumentNullException(nameof(createContext));
            }

            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            if (email == null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            var warningCutoff = cutoff.AddDays(1);
            await ProcessPagesAsync(
                createContext,
                "staging group",
                (entities, lastKey) => entities.StagingGroups.AsNoTracking()
                    .Where(group => group.Key > lastKey && group.ExpirationDate <= warningCutoff && !group.ActivePromotionId.HasValue)
                    .Where(group => group.ExpirationDate <= cutoff || (group.Owner.NotifyPackageStaged && !group.Owner.IsDeleted
                        && (!group.WarnedExpirationDate.HasValue || group.WarnedExpirationDate != group.ExpirationDate)))
                    .Select(group => group.Key),
                (entities, key) => ExpireGroupAsync(entities, key, cutoff, messages, email));
            await ProcessPagesAsync(
                createContext,
                "staged package",
                (entities, lastKey) => entities.StagedPackages.AsNoTracking()
                    .Where(attempt => attempt.Key > lastKey && attempt.ExpirationDate <= warningCutoff)
                    .Where(attempt => !attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
                    .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedPackageKey == attempt.Key)
                    .Where(attempt => attempt.StagedPackageIdentity.Package.PackageStatusKey == PackageStatus.Staged)
                    .Where(attempt => attempt.Status != StagedPackageStatus.Deleted && attempt.Status != StagedPackageStatus.Superseded && attempt.Status != StagedPackageStatus.Promoting)
                    .Where(attempt => attempt.ExpirationDate <= cutoff || (attempt.StagedPackageIdentity.Owner.NotifyPackageStaged && !attempt.StagedPackageIdentity.Owner.IsDeleted
                        && (!attempt.WarnedExpirationDate.HasValue || attempt.WarnedExpirationDate != attempt.ExpirationDate)))
                    .Select(attempt => attempt.Key),
                (entities, key) => ExpirePackageAsync(entities, key, cutoff, messages, email));
            await ProcessPagesAsync(
                createContext,
                "staged symbols",
                (entities, lastKey) => entities.StagedSymbolPackages.AsNoTracking()
                    .Where(attempt => attempt.Key > lastKey && attempt.ExpirationDate <= warningCutoff)
                    .Where(attempt => !attempt.StagedPackageIdentity.StagingGroupKey.HasValue)
                    .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key)
                    .Where(attempt => attempt.SymbolPackage.StatusKey == PackageStatus.Staged)
                    .Where(attempt => attempt.Status != StagedPackageStatus.Promoting)
                    .Where(attempt => attempt.ExpirationDate <= cutoff || (attempt.StagedPackageIdentity.Owner.NotifyPackageStaged && !attempt.StagedPackageIdentity.Owner.IsDeleted
                        && (!attempt.WarnedExpirationDate.HasValue || attempt.WarnedExpirationDate != attempt.ExpirationDate)))
                    .Select(attempt => attempt.Key),
                (entities, key) => ExpireSymbolsAsync(entities, key, cutoff, messages, email));
        }

        private async Task ProcessPagesAsync(
            Func<Task<IEntitiesContext>> createContext,
            string candidateType,
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
                    try
                    {
                        using (var entities = await createContext())
                        {
                            await expire(entities, key);
                        }
                    }
                    catch (DbUpdateConcurrencyException exception)
                    {
                        _logger.LogWarning(exception,
                            "Skipping expiration processing of {CandidateType} {CandidateKey} after a concurrency conflict. Later runs will recheck eligibility.",
                            candidateType, key);
                    }
                }
            }
        }

        private async Task ExpireGroupAsync(IEntitiesContext entities, int key, DateTime cutoff, IMessageService messages, MaintenanceEmailConfiguration email)
        {
            StagingExpirationMessage notification = null;
            var groups = new EntityRepository<StagingGroup>(entities);
            await groups.ExecuteInTransactionAsync(async () =>
            {
                var group = await groups.GetAll().Include(candidate => candidate.Owner).SingleOrDefaultAsync(candidate => candidate.Key == key);
                if (group == null || group.ExpirationDate > cutoff.AddDays(1) || group.ActivePromotionId.HasValue)
                {
                    _logger.LogInformation("Skipping removed, refreshed or promoting staging group {GroupKey}.", key);
                    return;
                }

                if (group.ExpirationDate > cutoff)
                {
                    if (!CanWarn(group.Owner, group.ExpirationDate, group.WarnedExpirationDate))
                    {
                        _logger.LogInformation("Skipping already warned or opted-out staging group {GroupKey}.", key);
                        return;
                    }

                    var hasPromotingPackage = await entities.StagedPackages
                        .Where(attempt => attempt.StagedPackageIdentity.StagingGroupKey == key)
                        .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedPackageKey == attempt.Key)
                        .AnyAsync(attempt => attempt.Status == StagedPackageStatus.Promoting);
                    if (hasPromotingPackage)
                    {
                        _logger.LogInformation("Skipping staging group {GroupKey} with a promoting package.", key);
                        return;
                    }

                    var hasPromotingSymbols = await entities.StagedSymbolPackages
                        .Where(attempt => attempt.StagedPackageIdentity.StagingGroupKey == key)
                        .Where(attempt => attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey == attempt.Key)
                        .AnyAsync(attempt => attempt.Status == StagedPackageStatus.Promoting);
                    if (hasPromotingSymbols)
                    {
                        _logger.LogInformation("Skipping staging group {GroupKey} with promoting symbols.", key);
                        return;
                    }

                    notification = CreateNotification(email, group.Owner, $"staging group {group.Name}", group.ExpirationDate, deleted: false);
                    group.WarnedExpirationDate = group.ExpirationDate;
                    await groups.CommitChangesAsync();
                    _logger.LogInformation("Recorded expiration warning for staging group {GroupKey}, deadline {ExpirationDate}.", key, group.ExpirationDate);
                    return;
                }

                var deleted = CreateNotification(email, group.Owner, $"staging group {group.Name}", group.ExpirationDate, deleted: true);
                var result = await CreateDeletionService(entities).DeleteGroupAsync(group);
                if (result.Type == StagingGroupDeletionResultType.Deleted)
                {
                    notification = deleted;
                }

                _logger.LogInformation("Expiration of staging group {GroupKey}: {Result}, {ArtifactCount} artifacts.", key, result.Type, result.AffectedPackageCount);
            });

            if (notification != null)
            {
                await messages.SendMessageAsync(notification);
            }
        }

        private async Task ExpirePackageAsync(IEntitiesContext entities, int key, DateTime cutoff, IMessageService messages, MaintenanceEmailConfiguration email)
        {
            StagingExpirationMessage notification = null;
            var packages = new EntityRepository<StagedPackage>(entities);
            await packages.ExecuteInTransactionAsync(async () =>
            {
                var attempt = await packages.GetAll()
                    .Include(candidate => candidate.StagedPackageIdentity.Package.PackageRegistration)
                    .Include(candidate => candidate.StagedPackageIdentity.CurrentStagedSymbolPackage.SymbolPackage)
                    .Include(candidate => candidate.StagedPackageIdentity.Owner)
                    .SingleOrDefaultAsync(candidate => candidate.Key == key);
                if (attempt == null)
                {
                    _logger.LogInformation("Skipping removed staged package {AttemptKey}.", key);
                    return;
                }

                var identity = attempt.StagedPackageIdentity;
                if (attempt.ExpirationDate > cutoff.AddDays(1) || identity.StagingGroupKey.HasValue || identity.CurrentStagedPackageKey != key)
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

                var description = $"staged package {identity.Package.PackageRegistration.Id} {identity.Package.NormalizedVersion}";
                if (attempt.ExpirationDate > cutoff)
                {
                    if (!CanWarn(identity.Owner, attempt.ExpirationDate, attempt.WarnedExpirationDate))
                    {
                        _logger.LogInformation("Skipping already warned or opted-out staged package {AttemptKey}.", key);
                        return;
                    }

                    notification = CreateNotification(email, identity.Owner, description, attempt.ExpirationDate, deleted: false);
                    attempt.WarnedExpirationDate = attempt.ExpirationDate;
                    await packages.CommitChangesAsync();
                    _logger.LogInformation("Recorded expiration warning for staged package {AttemptKey}, deadline {ExpirationDate}.", key, attempt.ExpirationDate);
                    return;
                }

                notification = CreateNotification(email, identity.Owner, description, attempt.ExpirationDate, deleted: true);
                await CreateDeletionService(entities).DeletePackageAsync(attempt);
                await packages.CommitChangesAsync();
                _logger.LogInformation("Expired staged package {AttemptKey}.", key);
            });

            if (notification != null)
            {
                await messages.SendMessageAsync(notification);
            }
        }

        private async Task ExpireSymbolsAsync(IEntitiesContext entities, int key, DateTime cutoff, IMessageService messages, MaintenanceEmailConfiguration email)
        {
            StagingExpirationMessage notification = null;
            var symbols = new EntityRepository<StagedSymbolPackage>(entities);
            await symbols.ExecuteInTransactionAsync(async () =>
            {
                var attempt = await symbols.GetAll()
                    .Include(candidate => candidate.SymbolPackage)
                    .Include(candidate => candidate.StagedPackageIdentity.CurrentStagedPackage)
                    .Include(candidate => candidate.StagedPackageIdentity.Package.PackageRegistration)
                    .Include(candidate => candidate.StagedPackageIdentity.Owner)
                    .SingleOrDefaultAsync(candidate => candidate.Key == key);
                if (attempt == null)
                {
                    _logger.LogInformation("Skipping removed staged symbols {AttemptKey}.", key);
                    return;
                }

                var identity = attempt.StagedPackageIdentity;
                if (attempt.ExpirationDate > cutoff.AddDays(1) || identity.StagingGroupKey.HasValue || identity.CurrentStagedSymbolPackageKey != key)
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

                var description = $"staged symbol package {identity.Package.PackageRegistration.Id} {identity.Package.NormalizedVersion}";
                if (attempt.ExpirationDate > cutoff)
                {
                    if (!CanWarn(identity.Owner, attempt.ExpirationDate, attempt.WarnedExpirationDate))
                    {
                        _logger.LogInformation("Skipping already warned or opted-out staged symbols {AttemptKey}.", key);
                        return;
                    }

                    notification = CreateNotification(email, identity.Owner, description, attempt.ExpirationDate, deleted: false);
                    attempt.WarnedExpirationDate = attempt.ExpirationDate;
                    await symbols.CommitChangesAsync();
                    _logger.LogInformation("Recorded expiration warning for staged symbols {AttemptKey}, deadline {ExpirationDate}.", key, attempt.ExpirationDate);
                    return;
                }

                notification = CreateNotification(email, identity.Owner, description, attempt.ExpirationDate, deleted: true);
                await CreateDeletionService(entities).DeleteSymbolPackageAsync(attempt);
                _logger.LogInformation("Expired staged symbols {AttemptKey}.", key);
            });

            if (notification != null)
            {
                await messages.SendMessageAsync(notification);
            }
        }

        private static bool CanWarn(User owner, DateTime expirationDate, DateTime? warnedExpirationDate)
        {
            return !owner.IsDeleted && owner.NotifyPackageStaged && warnedExpirationDate != expirationDate;
        }

        private static StagingExpirationMessage CreateNotification(MaintenanceEmailConfiguration email, User owner, string description, DateTime expirationDate, bool deleted)
        {
            return new StagingExpirationMessage(email, owner, description, expirationDate, deleted, email.ManagePackagesUrl, email.EmailSettingsUrl);
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

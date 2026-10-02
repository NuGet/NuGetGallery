// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace Gallery.Maintenance
{
    /// <summary>
    /// Queues old private staging files that have no staging attempt or cleanup reference.
    /// </summary>
    public class QueueOrphanedStagingBlobsTask : MaintenanceTask
    {
        private const int PageSize = 100;
        private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

        public QueueOrphanedStagingBlobsTask(ILogger<QueueOrphanedStagingBlobsTask> logger) : base(logger)
        {
        }

        public override async Task RunAsync(Job job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            var configuration = job.GetStagingBlobCleanupConfiguration();
            if (!configuration.Enabled)
            {
                _logger.LogInformation("Private staging orphan discovery is disabled.");
                return;
            }

            var container = job.CreateStagingBlobContainerClient(configuration);
            using (var entities = new EntitiesContext(await job.CreateSqlConnectionAsync<NuGet.Jobs.Configuration.GalleryDbConfiguration>(), readOnly: false))
            {
                await ProcessAsync(entities, container, DateTimeOffset.UtcNow);
            }
        }

        /// <summary>
        /// Queues unreferenced files last modified more than 24 hours before the scan started.
        /// </summary>
        public async Task ProcessAsync(IEntitiesContext entities, BlobContainerClient container, DateTimeOffset now)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (!(await container.ExistsAsync()).Value)
            {
                _logger.LogInformation("Private staging orphan discovery has no container to scan.");
                return;
            }

            var cleanupRepository = new EntityRepository<StagingBlobCleanup>(entities);
            var cutoff = now - MinimumAge;
            await foreach (var page in container.GetBlobsAsync().AsPages(pageSizeHint: PageSize))
            {
                var candidates = page.Values.Where(blob =>
                {
                    if (!blob.Properties.LastModified.HasValue)
                    {
                        throw new InvalidOperationException($"The staging blob '{blob.Name}' has no last-modified timestamp.");
                    }

                    return blob.Properties.LastModified.Value < cutoff;
                }).ToList();
                if (candidates.Count == 0)
                {
                    continue;
                }

                var paths = candidates.Select(blob => blob.Name).ToList();
                var referencedPaths = await entities.StagedPackages.Where(attempt => paths.Contains(attempt.UploadedBlobPath)).Select(attempt => attempt.UploadedBlobPath)
                    .Concat(entities.StagedPackages.Where(attempt => paths.Contains(attempt.ValidatedBlobPath)).Select(attempt => attempt.ValidatedBlobPath))
                    .Concat(entities.StagedSymbolPackages.Where(attempt => paths.Contains(attempt.UploadedBlobPath)).Select(attempt => attempt.UploadedBlobPath))
                    .Concat(cleanupRepository.GetAll().Where(request => paths.Contains(request.BlobPath)).Select(request => request.BlobPath))
                    .Distinct().ToListAsync();
                var references = new HashSet<string>(referencedPaths, StringComparer.Ordinal);
                var queuedCount = 0;
                foreach (var blob in candidates)
                {
                    if (references.Contains(blob.Name))
                    {
                        continue;
                    }

                    if (!blob.Properties.ETag.HasValue)
                    {
                        throw new InvalidOperationException($"The staging blob '{blob.Name}' has no ETag.");
                    }

                    cleanupRepository.InsertOnCommit(new StagingBlobCleanup
                    {
                        BlobPath = blob.Name,
                        BlobETag = blob.Properties.ETag.Value.ToString(),
                        QueuedDate = now.UtcDateTime,
                    });
                    queuedCount++;
                }

                if (queuedCount > 0)
                {
                    await cleanupRepository.CommitChangesAsync();
                    _logger.LogInformation("Queued {Count} orphaned private staging files for conditional cleanup.", queuedCount);
                }
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace Gallery.Maintenance
{
    /// <summary>
    /// Deletes obsolete private staging files using their captured ETags and retained-reference checks.
    /// </summary>
    public class DeleteStagingBlobsTask : MaintenanceTask
    {
        private const int BatchSize = 100;

        public DeleteStagingBlobsTask(ILogger<DeleteStagingBlobsTask> logger) : base(logger)
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
                _logger.LogInformation("Private staging blob cleanup is disabled.");
                return;
            }

            var container = job.CreateStagingBlobContainerClient(configuration);
            using (var entities = new EntitiesContext(await job.CreateSqlConnectionAsync<NuGet.Jobs.Configuration.GalleryDbConfiguration>(), readOnly: false))
            {
                await ProcessAsync(entities, container);
            }
        }

        /// <summary>
        /// Processes the private cleanup queue without starving requests behind retained or changed files.
        /// </summary>
        public async Task ProcessAsync(IEntitiesContext entities, BlobContainerClient container)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            var cleanupService = new StagingBlobCleanupService(entities);
            var cleanupRepository = new EntityRepository<StagingBlobCleanup>(entities);
            var lastKey = 0;
            while (true)
            {
                var requests = await cleanupRepository.GetAll()
                    .Where(request => request.Key > lastKey)
                    .OrderBy(request => request.Key)
                    .Take(BatchSize)
                    .ToListAsync();
                if (requests.Count == 0)
                {
                    return;
                }

                foreach (var request in requests)
                {
                    lastKey = request.Key;
                    if (cleanupService.HasLiveReference(request))
                    {
                        _logger.LogInformation("Deferring cleanup request {CleanupKey}: retained content at {BlobPath}.", request.Key, request.BlobPath);
                        continue;
                    }

                    var blob = container.GetBlobClient(request.BlobPath);
                    try
                    {
                        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, new BlobRequestConditions { IfMatch = new ETag(request.BlobETag) });
                    }
                    catch (RequestFailedException exception) when (exception.Status == 412)
                    {
                        // Conditional deletion of an already-missing blob can also return 412.
                        if ((await blob.ExistsAsync()).Value)
                        {
                            _logger.LogWarning(exception, "Retaining cleanup request {CleanupKey}: ETag mismatch at {BlobPath}.", request.Key, request.BlobPath);
                            continue;
                        }
                    }

                    cleanupRepository.DeleteOnCommit(request);
                    await cleanupRepository.CommitChangesAsync();
                    _logger.LogInformation("Completed cleanup request {CleanupKey} for {BlobPath}.", request.Key, request.BlobPath);
                }
            }
        }
    }
}

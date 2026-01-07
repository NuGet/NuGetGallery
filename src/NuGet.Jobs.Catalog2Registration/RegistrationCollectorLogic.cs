// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.V3;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationCollectorLogic : ICommitCollectorLogic
    {
        private readonly CommitCollectorUtility _utility;
        private readonly IRegistrationUpdater _updater;
        private readonly IOptionsSnapshot<Catalog2RegistrationConfiguration> _options;
        private readonly ILogger<RegistrationCollectorLogic> _logger;

        public RegistrationCollectorLogic(
            CommitCollectorUtility utility,
            IRegistrationUpdater updater,
            IOptionsSnapshot<Catalog2RegistrationConfiguration> options,
            ILogger<RegistrationCollectorLogic> logger)
        {
            _utility = utility ?? throw new ArgumentNullException(nameof(utility));
            _updater = updater ?? throw new ArgumentNullException(nameof(updater));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_options.Value.MaxConcurrentIds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"The {nameof(Catalog2RegistrationConfiguration.MaxConcurrentIds)} must be greater than zero.");
            }
        }

        public Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(IEnumerable<CatalogCommitItem> catalogItems)
        {
            // Create a single batch of all unprocessed catalog items so that we can have complete control of the
            // parallelism in this class.
            return Task.FromResult(_utility.CreateSingleBatch(catalogItems));
        }

        public async Task OnProcessBatchAsync(IEnumerable<CatalogCommitItem> items)
        {
            var itemList = items.ToList();
            _logger.LogInformation("Got {Count} catalog commit items to process.", itemList.Count);

            var latestItems = _utility.GetLatestPerIdentity(itemList);
            _logger.LogInformation("Got {Count} unique package identities.", latestItems.Count);

            var allWork = _utility.GroupById(latestItems);
            _logger.LogInformation("Got {Count} unique IDs.", allWork.Count);

            var allEntryToLeaf = await _utility.GetEntryToDetailsLeafAsync(latestItems);
            _logger.LogInformation("Fetched {Count} package details leaves.", allEntryToLeaf.Count);

            _logger.LogInformation("Starting {Count} workers processing each package ID batch.", _options.Value.MaxConcurrentIds);
            await ParallelAsync.Repeat(
                async () =>
                {
                    await Task.Yield();
                    while (allWork.TryTake(out var work))
                    {
                        var entryToLeaf = work
                            .Value
                            .Where(CommitCollectorUtility.IsOnlyPackageDetails)
                            .ToDictionary(e => e, e => allEntryToLeaf[e], ReferenceEqualityComparer<CatalogCommitItem>.Default);

                        await _updater.UpdateAsync(work.Id, work.Value, entryToLeaf);
                    }
                },
                _options.Value.MaxConcurrentIds);

            _logger.LogInformation("All workers have completed successfully.");
        }
    }
}

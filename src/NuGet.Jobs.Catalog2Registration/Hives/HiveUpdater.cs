// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Protocol.Catalog;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Versioning;

namespace NuGet.Jobs.Catalog2Registration
{
    public class HiveUpdater : IHiveUpdater
    {
        private static readonly IReadOnlyList<Uri> DeleteUris = new[] { Schema.DataTypes.PackageDelete };
        
        private readonly IHiveStorage _storage;
        private readonly IHiveMerger _merger;
        private readonly IEntityBuilder _entityBuilder;
        private readonly IOptionsSnapshot<Catalog2RegistrationConfiguration> _options;
        private readonly ILogger<HiveUpdater> _logger;

        public HiveUpdater(
            IHiveStorage storage,
            IHiveMerger merger,
            IEntityBuilder entityBuilder,
            IOptionsSnapshot<Catalog2RegistrationConfiguration> options,
            ILogger<HiveUpdater> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _merger = merger ?? throw new ArgumentNullException(nameof(merger));
            _entityBuilder = entityBuilder ?? throw new ArgumentNullException(nameof(entityBuilder));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateAsync(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            string id,
            IReadOnlyList<CatalogCommitItem> entries,
            IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> entryToCatalogLeaf,
            CatalogCommit registrationCommit)
        {
            // Validate the input and put it in more convenient forms.
            if (!entries.Any())
            {
                return;
            }
            GuardInput(entries, entryToCatalogLeaf);
            var sortedCatalog = entries.OrderBy(x => x.PackageIdentity.Version).ToList();
            var versionToCatalogLeaf = entryToCatalogLeaf.ToDictionary(x => x.Key.PackageIdentity.Version, x => x.Value);

            // Remove SemVer 2.0.0 versions if this hive should only have SemVer 1.0.0 versions.
            if (ShouldExcludeSemVer2(hive))
            {
                Guard.Assert(
                    replicaHives.All(ShouldExcludeSemVer2),
                    "A replica hive of a non-SemVer 2.0.0 hive must also exclude SemVer 2.0.0.");

                ExcludeSemVer2(hive, sortedCatalog, versionToCatalogLeaf);
            }
            else
            {
                Guard.Assert(
                    replicaHives.All(x => !ShouldExcludeSemVer2(x)),
                    "A replica hive of a SemVer 2.0.0 hive must also include SemVer 2.0.0.");
            }

            _logger.LogInformation(
                "Starting to update the {PackageId} registration index in the {Hive} hive and {ReplicaHives} replica hives with {UpsertCount} " +
                "package details and {DeleteCount} package deletes.",
                id,
                hive,
                replicaHives,
                entryToCatalogLeaf.Count,
                entries.Count - entryToCatalogLeaf.Count);

            // Read the existing registration index if it exists. If it does not exist, initialize a new index.
            var index = await _storage.ReadIndexOrNullAsync(hive, id);
            IndexInfo indexInfo;
            if (index == null)
            {
                indexInfo = IndexInfo.New();
            }
            else
            {
                indexInfo = IndexInfo.Existing(_storage, hive, index);
            }

            // Find all of the existing page URLs. This will be used later to find orphan pages.
            var existingPageUrls = GetPageUrls(indexInfo);

            // Read all of the obviously relevant pages in parallel. This simply evaluates some work that would
            // otherwise be done lazily.
            await LoadRelevantPagesAsync(sortedCatalog, indexInfo);

            // Merge the incoming catalog entries in memory.
            var mergeResult = await _merger.MergeAsync(indexInfo, sortedCatalog);

            // Write the modified leaves.
            await UpdateLeavesAsync(hive, replicaHives, id, versionToCatalogLeaf, registrationCommit, mergeResult);

            // Write the pages and handle the inline vs. non-inlined cases.
            if (indexInfo.Items.Count == 0)
            {
                _logger.LogInformation("There are no pages to update.");
            }
            else
            {
                var itemCount = indexInfo.Items.Sum(x => x.Count);
                if (itemCount <= _options.Value.MaxInlinedLeafItems)
                {
                    _logger.LogInformation(
                        "There are {Count} total leaf items so the leaf items will be inlined.",
                        itemCount);

                    await UpdateInlinedPagesAsync(hive, id, indexInfo, registrationCommit);
                }
                else
                {
                    _logger.LogInformation(
                        "There are {Count} total leaf items so the leaf items will not be inlined.",
                        itemCount);

                    await UpdateNonInlinedPagesAsync(hive, replicaHives, id, indexInfo, registrationCommit, mergeResult);
                }
            }

            // Write the index, if there were any changes.
            if (mergeResult.ModifiedPages.Any() || mergeResult.ModifiedLeaves.Any())
            {
                _logger.LogInformation("Updating the index.");
                _entityBuilder.UpdateIndex(indexInfo.Index, hive, id, indexInfo.Items.Count);
                _entityBuilder.UpdateCommit(indexInfo.Index, registrationCommit);
                await _storage.WriteIndexAsync(hive, replicaHives, id, indexInfo.Index);
            }

            if (!indexInfo.Items.Any())
            {
                _logger.LogInformation("Deleting the index since there are no more page items.");
                await _storage.DeleteIndexAsync(hive, replicaHives, id);
            }

            // Delete orphan blobs.
            await DeleteOrphansAsync(hive, replicaHives, existingPageUrls, indexInfo, mergeResult);

            _logger.LogInformation(
                "Done updating the {PackageId} registration index in the {Hive} hive and replica hives {ReplicaHives}. {ModifiedPages} pages were " +
                "updated, {ModifiedLeaves} leaves were upserted, and {DeletedLeaves} leaves were deleted.",
                id,
                hive,
                replicaHives,
                mergeResult.ModifiedPages.Count,
                mergeResult.ModifiedLeaves.Count,
                mergeResult.DeletedLeaves.Count);
        }

        private static bool ShouldExcludeSemVer2(HiveType hive)
        {
            return hive == HiveType.Legacy || hive == HiveType.Gzipped;
        }

        private void ExcludeSemVer2(
            HiveType hive,
            List<CatalogCommitItem> sortedCatalog,
            Dictionary<NuGetVersion, PackageDetailsCatalogLeaf> versionToCatalogLeaf)
        {
            Guard.Assert(
                hive == HiveType.Legacy || hive == HiveType.Gzipped,
                "Only the legacy and gzipped hives should exclude SemVer 2.0.0 versions.");

            for (int i = 0; i < sortedCatalog.Count; i++)
            {
                var catalogCommitItem = sortedCatalog[i];
                if (catalogCommitItem.IsPackageDelete)
                {
                    continue;
                }

                var version = catalogCommitItem.PackageIdentity.Version;
                var catalogLeaf = versionToCatalogLeaf[version];
                if (catalogLeaf.IsSemVer2())
                {
                    // Turn the PackageDetails into a PackageDelete to ensure that a known SemVer 2.0.0 version is not
                    // in a hive that should not have SemVer 2.0.0. This may cause a little bit more work (like a
                    // non-inlined page getting downloaded) but it's worth it to allow reflows of SemVer 2.0.0 packages
                    // to fix up problems. In general, reflow should be a powerful fix-up tool. If this causes
                    // performance issues later, we can revisit this extra work at that time.
                    sortedCatalog[i] = new CatalogCommitItem(
                        catalogCommitItem.Uri,
                        catalogCommitItem.CommitId,
                        catalogCommitItem.CommitTimeStamp,
                        types: null,
                        typeUris: DeleteUris,
                        packageIdentity: catalogCommitItem.PackageIdentity);
                    versionToCatalogLeaf.Remove(version);

                    _logger.LogInformation(
                        "Version {Version} is SemVer 2.0.0 so it will be treated as a package delete for the {Hive} hive.",
                        catalogLeaf.ParsePackageVersion().ToFullString(),
                        hive);
                }
            }
        }

        private async Task LoadRelevantPagesAsync(List<CatalogCommitItem> sortedCatalog, IndexInfo indexInfo)
        {
            // If there are no page items at all, there is no work to do.
            if (indexInfo.Items.Count == 0)
            {
                return;
            }

            var catalogIndex = 0;
            var pageIndex = 0;
            var relevantPages = new ConcurrentBag<PageInfo>();

            // Load pages where at least one catalog item falls in the bounds of the page.
            while (catalogIndex < sortedCatalog.Count && pageIndex < indexInfo.Items.Count)
            {
                var currentCatalog = sortedCatalog[catalogIndex];
                var currentPage = indexInfo.Items[pageIndex];

                if (currentCatalog.PackageIdentity.Version < currentPage.Lower)
                {
                    // The current catalog item lower than the current page's bounds. Move on to the next catalog item.
                    catalogIndex++;
                }
                else if (currentCatalog.PackageIdentity.Version <= currentPage.Upper)
                {
                    // The current catalog item is inside the current page's bounds. This page should be downloaded.
                    if (!currentPage.IsInlined)
                    {
                        _logger.LogInformation(
                            "Preemptively loading page {PageNumber}/{PageCount} [{Lower}, {Upper}] since catalog version {Version} is in its bounds.",
                            pageIndex + 1,
                            indexInfo.Items.Count,
                            currentPage.Lower.ToNormalizedString(),
                            currentPage.Upper.ToNormalizedString(),
                            currentCatalog.PackageIdentity.Version.ToNormalizedString());

                        relevantPages.Add(currentPage);
                    }

                    // This page is now included in the set of relevant pages. No need to consider it any more.
                    pageIndex++;
                }
                else
                {
                    // The current catalog item is higher than the current page's bounds. This page is not relevant.
                    pageIndex++;
                }
            }

            await ParallelAsync.Repeat(
                async () =>
                {
                    await Task.Yield();
                    while (relevantPages.TryTake(out var pageInfo))
                    {
                        await pageInfo.GetLeafInfosAsync();
                    }
                },
                _options.Value.MaxConcurrentOperationsPerHive);
        }

        private async Task UpdateLeavesAsync(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            string id,
            Dictionary<NuGetVersion, PackageDetailsCatalogLeaf> versionToCatalogLeaf,
            CatalogCommit registrationCommit,
            HiveMergeResult mergeResult)
        {
            if (!mergeResult.ModifiedLeaves.Any())
            {
                _logger.LogInformation("No leaves need to be updated.");
                return;
            }

            _logger.LogInformation(
                "Updating {Count} registration leaves.",
                mergeResult.ModifiedLeaves.Count,
                id,
                hive);

            var taskFactories = new ConcurrentBag<Func<Task>>();
            foreach (var leafInfo in mergeResult.ModifiedLeaves)
            {
                _entityBuilder.UpdateLeafItem(leafInfo.LeafItem, hive, id, versionToCatalogLeaf[leafInfo.Version]);
                _entityBuilder.UpdateCommit(leafInfo.LeafItem, registrationCommit);
                var leaf = _entityBuilder.NewLeaf(leafInfo.LeafItem);
                taskFactories.Add(async () =>
                {
                    _logger.LogInformation("Updating leaf {PackageId} {Version}.", id, leafInfo.Version.ToNormalizedString());
                    await _storage.WriteLeafAsync(hive, replicaHives, id, leafInfo.Version, leaf);
                });
            }

            await ParallelAsync.Repeat(
                async () =>
                {
                    await Task.Yield();
                    while (taskFactories.TryTake(out var taskFactory))
                    {
                        await taskFactory();
                    }
                },
                _options.Value.MaxConcurrentOperationsPerHive);
        }

        private async Task UpdateInlinedPagesAsync(
            HiveType hive,
            string id,
            IndexInfo indexInfo,
            CatalogCommit registrationCommit)
        {
            for (var pageIndex = 0; pageIndex < indexInfo.Items.Count; pageIndex++)
            {
                var pageInfo = indexInfo.Items[pageIndex];

                if (!pageInfo.IsInlined)
                {
                    _logger.LogInformation(
                        "Moving page {PageNumber}/{PageCount} [{Lower}, {Upper}] from having its own blob to being inlined.",
                        pageIndex + 1,
                        indexInfo.Items.Count,
                        pageInfo.Lower.ToNormalizedString(),
                        pageInfo.Upper.ToNormalizedString());

                    pageInfo = await pageInfo.CloneToInlinedAsync();
                    indexInfo.RemoveAt(pageIndex);
                    indexInfo.Insert(pageIndex, pageInfo);
                }

                Guard.Assert(pageInfo.IsInlined, "The page should be inlined at this point.");

                _entityBuilder.UpdateInlinedPageItem(pageInfo.PageItem, hive, id, pageInfo.Count, pageInfo.Lower, pageInfo.Upper);
                _entityBuilder.UpdateCommit(pageInfo.PageItem, registrationCommit);
            }
        }

        private async Task UpdateNonInlinedPagesAsync(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            string id,
            IndexInfo indexInfo,
            CatalogCommit registrationCommit,
            HiveMergeResult mergeResult)
        {
            var taskFactories = new ConcurrentBag<Func<Task>>();
            for (var pageIndex = 0; pageIndex < indexInfo.Items.Count; pageIndex++)
            {
                var pageInfo = indexInfo.Items[pageIndex];

                if (pageInfo.IsInlined)
                {
                    _logger.LogInformation(
                        "Moving page {PageNumber}/{PageCount} [{Lower}, {Upper}] from being inlined to having its own blob.",
                        pageIndex + 1,
                        indexInfo.Items.Count,
                        pageInfo.Lower.ToNormalizedString(),
                        pageInfo.Upper.ToNormalizedString());

                    pageInfo = await pageInfo.CloneToNonInlinedAsync();
                    indexInfo.RemoveAt(pageIndex);
                    indexInfo.Insert(pageIndex, pageInfo);
                }
                else if (!mergeResult.ModifiedPages.Contains(pageInfo))
                {
                    _logger.LogInformation(
                        "Skipping unmodified page {PageNumber}/{PageCount} [{Lower}, {Upper}].",
                        pageIndex + 1,
                        indexInfo.Items.Count,
                        pageInfo.Lower.ToNormalizedString(),
                        pageInfo.Upper.ToNormalizedString());

                    continue;
                }

                Guard.Assert(!pageInfo.IsInlined, "The page should not be inlined at this point.");

                var page = await pageInfo.GetPageAsync();
                _entityBuilder.UpdateNonInlinedPageItem(pageInfo.PageItem, hive, id, pageInfo.Count, pageInfo.Lower, pageInfo.Upper);
                _entityBuilder.UpdateCommit(pageInfo.PageItem, registrationCommit);
                _entityBuilder.UpdatePage(page, hive, id, pageInfo.Count, pageInfo.Lower, pageInfo.Upper);
                _entityBuilder.UpdateCommit(page, registrationCommit);

                var pageNumber = pageIndex + 1;
                taskFactories.Add(async () =>
                {
                    _logger.LogInformation(
                        "Updating page {PageNumber}/{PageCount} [{Lower}, {Upper}].",
                        pageNumber,
                        indexInfo.Items.Count,
                        pageInfo.Lower.ToNormalizedString(),
                        pageInfo.Upper.ToNormalizedString());
                    await _storage.WritePageAsync(hive, replicaHives, id, pageInfo.Lower, pageInfo.Upper, page);
                });
            }

            await ParallelAsync.Repeat(
                async () =>
                {
                    await Task.Yield();
                    while (taskFactories.TryTake(out var taskFactory))
                    {
                        await taskFactory();
                    }
                },
                _options.Value.MaxConcurrentOperationsPerHive);
        }

        private static void GuardInput(
            IReadOnlyList<CatalogCommitItem> entries,
            IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> entryToCatalogLeaf)
        {
            var uniqueVersions = new HashSet<NuGetVersion>();
            foreach (var entry in entries)
            {
                Guard.Assert(
                    entry.IsPackageDelete ^ entry.IsPackageDetails,
                    "A catalog commit item must be either a PackageDelete or a package details but not both.");
                Guard.Assert(
                    uniqueVersions.Add(entry.PackageIdentity.Version),
                    "There must be exactly on catalog commit item per version.");

                if (entry.IsPackageDetails)
                {
                    Guard.Assert(
                        entryToCatalogLeaf.ContainsKey(entry),
                        "Each PackageDetails catalog commit item must have an associate catalog leaf.");
                }
            }
        }

        private HashSet<string> GetPageUrls(IndexInfo indexInfo)
        {
            return new HashSet<string>(indexInfo
                .Items
                .Where(x => !x.IsInlined)
                .Select(x => x.PageItem.Url));
        }

        private async Task DeleteOrphansAsync(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            IEnumerable<string> existingPageUrls,
            IndexInfo indexInfo,
            HiveMergeResult mergeResult)
        {
            // Start with all of the page URLs found in the index prior to the update process.
            var orphanUrls = new HashSet<string>(existingPageUrls);

            // Add all of the deleted leaf URLs.
            orphanUrls.UnionWith(mergeResult.DeletedLeaves.Select(x => x.LeafItem.Url));

            // Leave the new page URLs alone.
            foreach (var pageInfo in indexInfo.Items)
            {
                orphanUrls.Remove(pageInfo.PageItem.Url);
            }

            // Leave the modified leaf URLs alone. This should not be necessary since deleted leaves and modified
            // leaves are disjoint sets but is a reasonable precaution.
            foreach (var leafInfo in mergeResult.ModifiedLeaves)
            {
                orphanUrls.Remove(leafInfo.LeafItem.Url);
            }

            if (orphanUrls.Count == 0)
            {
                _logger.LogInformation("There are no orphan blobs to delete.");
                return;
            }

            _logger.LogInformation("About to delete {Count} orphan blobs.", orphanUrls.Count);
            var work = new ConcurrentBag<string>(orphanUrls);
            await ParallelAsync.Repeat(
                async () =>
                {
                    while (work.TryTake(out var url))
                    {
                        await _storage.DeleteUrlAsync(hive, replicaHives, url);
                    }
                },
                _options.Value.MaxConcurrentOperationsPerHive);
            _logger.LogInformation("Done deleting orphan blobs.", orphanUrls.Count);
        }
    }
}

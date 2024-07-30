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

namespace NuGet.Jobs.Catalog2Registration
{
    public class HiveMerger : IHiveMerger
    {
        private readonly IOptionsSnapshot<Catalog2RegistrationConfiguration> _options;
        private readonly ILogger<HiveMerger> _logger;

        public HiveMerger(
            IOptionsSnapshot<Catalog2RegistrationConfiguration> options,
            ILogger<HiveMerger> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private int MaxLeavesPerPage => _options.Value.MaxLeavesPerPage;

        public async Task<HiveMergeResult> MergeAsync(IndexInfo indexInfo, IReadOnlyList<CatalogCommitItem> sortedCatalog)
        {
            for (var i = 1; i < sortedCatalog.Count; i++)
            {
                Guard.Assert(
                    sortedCatalog[i - 1].PackageIdentity.Version < sortedCatalog[i].PackageIdentity.Version,
                    "The catalog commit items must be in ascending order by version.");
            }

            var context = new Context(indexInfo, sortedCatalog);

            await MergeAsync(context);

            return new HiveMergeResult(
                context.ModifiedPages,
                context.ModifiedLeaves,
                context.DeletedLeaves);
        }

        private async Task MergeAsync(Context context)
        {
            // The general approach here is to use the merge algorithm to combine the incoming list of catalog commit
            // items with the existing list of registration leaf items to end up with a new sorted list of registration
            // leaf items. There are additional complexities in addition a textbook "merge" algorithm:
            //
            //   1. Items from the catalog of type PackageDelete can result in a removal from the result list.
            //
            //   2. Items from the catalog can be ignored if it is a PackageDelete on a version that does not exist in
            //      the list of registration leaf items.
            //
            //   3. When both input sorted lists (catalog and registration) have the same version, the item from the
            //      catalog replaces the item from the registration instead of having both versions next to each other.
            //
            //   4. There is additional bookkeeping requires to manage pages. We have a maximum number of leaves per
            //      registration page so we need to handle cases when a full page needs to be filled up again after
            //      a package delete or have items pushed to a later page if it is too full (from a package insert).
            //
            // These are just complexities though. The general approach of "merge" still works fine. The catalog leaves
            // can be sorted by version in memory and the registration leaves are already sorted. We will iterate
            // through the two lists in parallel by using queue data structures.
            //
            // The benefit of the merge algorithm is that we don't need to touch registration pages that don't bound
            // any of the incoming catalog leaves.
            //
            // See the wonderful Wikipedia page: https://en.wikipedia.org/wiki/Merge_algorithm

            var catalogIndex = 0;
            var pageIndex = 0;

            var sortedCatalog = context.SortedCatalog;
            var pageInfos = context.IndexInfo.Items;

            // Keep track of the position in the current page.
            var itemIndex = 0;

            // This is the main "merge" step where the two input lists are interleaved.
            while (catalogIndex < sortedCatalog.Count && pageIndex < pageInfos.Count)
            {
                var catalog = sortedCatalog[catalogIndex];
                var pageInfo = pageInfos[pageIndex];
                var nextLower = pageIndex < pageInfos.Count - 1 ? pageInfos[pageIndex + 1].Lower : null;
                var hasGap = pageInfo.Count < MaxLeavesPerPage && nextLower != null && catalog.PackageIdentity.Version < nextLower;

                if (catalog.PackageIdentity.Version <= pageInfo.Upper || hasGap)
                {
                    itemIndex = await MergeEntryIntoPageAsync(context, catalog, pageIndex, itemIndex);
                    catalogIndex++;
                }
                else
                {
                    // Before considering the current page "complete" ensure the page size is correct.
                    await EnsureValidPageSizeAtAsync(context, pageIndex);

                    if (catalog.PackageIdentity.Version > pageInfo.Upper)
                    {
                        // The current catalog entry is greater than the current page's upper bound. This means we're done
                        // with the current page. Move to the next page and reset the item index.
                        pageIndex++;
                        itemIndex = 0;
                    }
                }
            }

            // Now that one of the two input lists is drained, handle the other (undrained) list. 

            // Process the rest of the catalog leaves, if any.
            if (catalogIndex < sortedCatalog.Count)
            {
                // Make sure there is at least one non-full page so that remaining catalog leaves can be pushed there.
                if (pageInfos.Count == 0
                    || pageInfos.Last().Count == MaxLeavesPerPage)
                {
                    context.IndexInfo.Insert(pageIndex, PageInfo.New());
                }
                else
                {
                    pageIndex = pageInfos.Count - 1;
                }

                // Push the remaining catalog leaves into the last page.
                while (catalogIndex < sortedCatalog.Count)
                {
                    var catalog = sortedCatalog[catalogIndex];
                    itemIndex = await MergeEntryIntoPageAsync(context, catalog, pageIndex, itemIndex);
                    catalogIndex++;
                }

                RemovePageAtIfEmpty(context, pageIndex);
            }

            // Process the rest of the registration pages, if any, by ensuring the remaining pages are not too large.
            while (pageIndex < pageInfos.Count)
            {
                await EnsureValidPageSizeAtAsync(context, pageIndex);
                pageIndex++;
            }
        }

        private async Task<int> MergeEntryIntoPageAsync(
            Context context,
            CatalogCommitItem entry,
            int pageIndex,
            int itemIndex)
        {
            var pageInfo = context.IndexInfo.Items[pageIndex];
            var items = await pageInfo.GetLeafInfosAsync();

            while (itemIndex < items.Count)
            {
                if (entry.PackageIdentity.Version > items[itemIndex].Version)
                {
                    // The current position in the item list is too low for the catalog version. Keep looking.
                    itemIndex++;
                }
                else if (entry.PackageIdentity.Version == items[itemIndex].Version)
                {
                    if (entry.IsPackageDelete)
                    {
                        // Remove the registration leaf item. The current catalog commit item represents a
                        // delete for this version.
                        _logger.LogInformation(
                            "Version {Version} will be deleted by commit {CommitId}.",
                            entry.PackageIdentity.Version.ToNormalizedString(),
                            entry.CommitId);
                        context.DeletedLeaves.Add(await pageInfo.RemoveAtAsync(itemIndex));
                        context.ModifiedPages.Add(pageInfo);

                        RemovePageAtIfEmpty(context, pageIndex);
                    }
                    else
                    {
                        // Update the metadata of the existing registration leaf item.
                        _logger.LogInformation(
                            "Version {Version} will be updated by commit {CommitId}.",
                            entry.PackageIdentity.Version.ToNormalizedString(),
                            entry.CommitId);
                        context.ModifiedLeaves.Add(items[itemIndex]);
                        context.ModifiedPages.Add(pageInfo);
                    }

                    // The version has been matched with an existing version. Leave the item index as-is. The next item
                    // is now at the current position. No more work is necessary. 
                    return itemIndex;
                }
                else
                {
                    break;
                }
            }

            await InsertAsync(context, pageInfo, itemIndex, entry);

            return itemIndex;
        }

        private async Task InsertAsync(
            Context context,
            PageInfo pageInfo,
            int index,
            CatalogCommitItem entry)
        {
            if (entry.IsPackageDelete)
            {
                // No matching version was found for this delete. No more work is necessary.
                _logger.LogInformation(
                    "Version {Version} does not exist. The delete from commit {CommitId} will have no affect.",
                    entry.PackageIdentity.Version.ToNormalizedString(),
                    entry.CommitId);
            }
            else
            {
                // Insert the new registration leaf item.
                _logger.LogInformation(
                    "Version {Version} will be added by commit {CommitId}.",
                    entry.PackageIdentity.Version.ToNormalizedString(),
                    entry.CommitId);
                var leafInfo = LeafInfo.New(entry.PackageIdentity.Version);
                await pageInfo.InsertAsync(index, leafInfo);
                context.ModifiedLeaves.Add(leafInfo);
                context.ModifiedPages.Add(pageInfo);
            }
        }

        private async Task EnsureValidPageSizeAtAsync(Context context, int pageIndex)
        {
            var pageInfo = context.IndexInfo.Items[pageIndex];

            // If we're not on the last page, pull items from the next page until the page is full or we've drained all
            // of the subsequent pages and are now the last page.
            while (pageInfo.Count < MaxLeavesPerPage && pageIndex < context.IndexInfo.Items.Count - 1)
            {
                var nextPageInfo = context.IndexInfo.Items[pageIndex + 1];
                var leafInfo = await nextPageInfo.RemoveAtAsync(0);
                _logger.LogInformation(
                    "Page {PageNumber}/{PageCount} has too few items. Version {Version} will be moved from the next page.",
                    pageIndex + 1,
                    context.IndexInfo.Items.Count,
                    leafInfo.Version);
                await pageInfo.InsertAsync(pageInfo.Count, leafInfo);
                context.ModifiedPages.Add(pageInfo);
                context.ModifiedPages.Add(nextPageInfo);
                RemovePageAtIfEmpty(context, pageIndex + 1);
            }

            // If the page is too large, push the extra items to the next page.
            while (pageInfo.Count > MaxLeavesPerPage)
            {
                PageInfo nextPageInfo;
                if (pageIndex == context.IndexInfo.Items.Count - 1)
                {
                    nextPageInfo = PageInfo.New();
                    context.IndexInfo.Insert(context.IndexInfo.Items.Count, nextPageInfo);
                }
                else
                {
                    nextPageInfo = context.IndexInfo.Items[pageIndex + 1];
                }

                var leafInfo = await pageInfo.RemoveAtAsync(pageInfo.Count - 1);
                _logger.LogInformation(
                    "Page {PageNumber}/{PageCount} has too many items. Version {Version} will be moved to the next page.",
                    pageIndex + 1,
                    context.IndexInfo.Items.Count,
                    leafInfo.Version);
                await nextPageInfo.InsertAsync(0, leafInfo);
                context.ModifiedPages.Add(pageInfo);
                context.ModifiedPages.Add(nextPageInfo);
            }
        }

        private void RemovePageAtIfEmpty(Context context, int pageIndex)
        {
            var pageInfo = context.IndexInfo.Items[pageIndex];
            if (pageInfo.Count == 0)
            {
                _logger.LogInformation(
                    "The last version on page {PageNumber}/{PageCount} has been removed. The page itself will be removed.",
                    pageIndex + 1,
                    context.IndexInfo.Items.Count);
                context.IndexInfo.RemoveAt(pageIndex);
                context.ModifiedPages.Remove(pageInfo);
            }
        }

        private class Context
        {
            public Context(IndexInfo indexInfo, IReadOnlyList<CatalogCommitItem> sortedCatalog)
            {
                SortedCatalog = sortedCatalog;
                IndexInfo = indexInfo;
                ModifiedPages = new HashSet<PageInfo>();
                ModifiedLeaves = new HashSet<LeafInfo>();
                DeletedLeaves = new HashSet<LeafInfo>();
            }

            public IndexInfo IndexInfo { get; }
            public IReadOnlyList<CatalogCommitItem> SortedCatalog { get; }
            public HashSet<PageInfo> ModifiedPages { get; }
            public HashSet<LeafInfo> ModifiedLeaves { get; }
            public HashSet<LeafInfo> DeletedLeaves { get; }
        }
    }
}

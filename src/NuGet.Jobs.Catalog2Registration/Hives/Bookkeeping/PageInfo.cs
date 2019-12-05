// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Protocol.Registration;
using NuGet.Services;
using NuGet.Versioning;

namespace NuGet.Jobs.Catalog2Registration
{
    /// <summary>
    /// A class that handles the bookkeeping of a registration page. This contains the page item for easy access but
    /// also has the logic to fetch the leaf items if they are not inlined.
    /// 
    /// The count and bounds properties are updated by the <see cref="InsertAsync(int, LeafInfo)"/> and
    /// <see cref="RemoveAtAsync(int)"/> methods as items are moved around but note that the only property on the
    /// <see cref="PageItem"/> (or external page) updated by this bookkeeping class is
    /// <see cref="RegistrationPage.Items"/>. The other properties can be updated by the caller prior to serialization
    /// as necessary.
    /// 
    /// The main benefit of a dedicated bookkeeping class is that it can hold parsed version instances and can keep
    /// them up to date given relevant state-changing actions. For example, removing a leaf item changes the count and
    /// can change the bounds.
    /// 
    /// In general pages are ephemeral things that don't hold any unique state than can't be inferred by the contained
    /// leaf items.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class PageInfo
    {
        private string DebuggerDisplay =>
            $"Page " +
            $"[{Lower?.ToNormalizedString() ?? "null"}, {Upper?.ToNormalizedString() ?? "null"}] " +
            $"({Count} leaf items)";

        private readonly Lazy<Task> _lazyInitializeTask;
        private RegistrationPage _page;
        private List<LeafInfo> _leafInfos;

        private PageInfo(
            RegistrationPage pageItem,
            Func<PageInfo, Task> initializeTaskFactory)
        {
            PageItem = pageItem ?? throw new ArgumentNullException(nameof(pageItem));

            if (initializeTaskFactory == null)
            {
                throw new ArgumentNullException(nameof(initializeTaskFactory));
            }

            _lazyInitializeTask = new Lazy<Task>(() => initializeTaskFactory(this));
        }

        public static PageInfo New()
        {
            var pageItem = new RegistrationPage
            {
                Items = new List<RegistrationLeafItem>(),
            };

            var pageInfo = new PageInfo(pageItem, _ => Task.CompletedTask);
            Initialize(pageInfo, pageItem);
            return pageInfo;
        }

        public static PageInfo Existing(
            RegistrationPage pageItem,
            Func<string, Task<RegistrationPage>> getPageByUrlAsync)
        {
            if (pageItem.Items == null)
            {
                if (getPageByUrlAsync == null)
                {
                    throw new ArgumentNullException(nameof(getPageByUrlAsync));
                }

                return new PageInfo(
                    pageItem,
                    async pageInfo =>
                    {
                        var page = await getPageByUrlAsync(pageItem.Url);
                        Initialize(pageInfo, page);
                    })
                {
                    Count = pageItem.Count,
                    Lower = NuGetVersion.Parse(pageItem.Lower),
                    Upper = NuGetVersion.Parse(pageItem.Upper),
                };
            }
            else
            {
                var pageInfo = new PageInfo(pageItem, _ => Task.CompletedTask);
                Initialize(pageInfo, pageItem);
                return pageInfo;
            }
        }

        public bool IsInlined => PageItem.Items != null;
        public int Count { get; private set; }
        public NuGetVersion Lower { get; private set; }
        public NuGetVersion Upper { get; private set; }
        public RegistrationPage PageItem { get; set; }
        public bool IsPageFetched => _lazyInitializeTask.IsValueCreated;

        private static void Initialize(PageInfo pageInfo, RegistrationPage page)
        {
            // Ensure the page is sorted in ascending order by version.
            var leafInfos = page
                .Items
                .Select(x => LeafInfo.Existing(x))
                .OrderBy(x => x.Version)
                .ToList();
            page.Items.Clear();
            page.Items.AddRange(leafInfos.Select(x => x.LeafItem));

            // Update the bookkeeping with the latest information. The leaf items themselves are the "true" for the
            // count, lower bound, and upper bound properties not whatever might be set on the page item.
            pageInfo._page = page;
            pageInfo._leafInfos = leafInfos;
            pageInfo.Count = page.Items.Count;
            pageInfo.Lower = leafInfos.FirstOrDefault()?.Version;
            pageInfo.Upper = leafInfos.LastOrDefault()?.Version;
        }

        /// <summary>
        /// Clone this page info into another instance but with the leaf items inlined. An inlined page is one that has
        /// its leaf items in the page item, not in an external page.
        /// </summary>
        /// <returns>The new page info instance with leaf items inlined.</returns>
        public async Task<PageInfo> CloneToInlinedAsync()
        {
            var page = await GetPageAsync();
            return Existing(page, getPageByUrlAsync: null);
        }

        /// <summary>
        /// Clone this page info into another instance but with the leaf items not inlined. A non-inlined page is one
        /// that has a null item list in the page item. The leaf items are stored in an external page.
        /// </summary>
        /// <returns>The new page info instance with leaf items in a different page instance.</returns>
        public async Task<PageInfo> CloneToNonInlinedAsync()
        {
            var page = await GetPageAsync();
            var pageInfo = new PageInfo(new RegistrationPage(), _ => Task.CompletedTask);
            Initialize(pageInfo, page);
            return pageInfo;
        }

        public async Task<LeafInfo> RemoveAtAsync(int index)
        {
            var page = await GetPageAsync();
            var leafInfos = await GetMutableLeafInfosAsync();

            // Remove from the real page, for future serialization.
            page.Items.RemoveAt(index);

            // Remove from the leaf info list, for bookkeeping.
            var leafInfo = leafInfos[index];
            leafInfos.RemoveAt(index);

            Count--;
            UpdateBounds(page, leafInfos);

            return leafInfo;
        }

        public async Task InsertAsync(int index, LeafInfo leafInfo)
        {
            var page = await GetPageAsync();
            var leafInfos = await GetMutableLeafInfosAsync();

            if (index > 0)
            {
                Guard.Assert(
                    leafInfos[index - 1].Version < leafInfo.Version,
                    "The version added to a page must have a higher version than the item before it.");
            }

            if (index < leafInfos.Count)
            {
                Guard.Assert(
                    leafInfos[index].Version > leafInfo.Version,
                    "The version added to a page must have a lower version than the item after it.");
            }

            // Add to the real page, for future serialization.
            page.Items.Insert(index, leafInfo.LeafItem);

            // Add to the leaf info list, for bookeeping.
            leafInfos.Insert(index, leafInfo);

            Count++;
            UpdateBounds(page, leafInfos);
        }

        private void UpdateBounds(RegistrationPage page, List<LeafInfo> leafInfos)
        {
            Guard.Assert(Count == page.Items.Count, "The count property on the page info must match the number of leaf items.");
            Guard.Assert(Count == leafInfos.Count, "The count property on the page info match the number of leaf infos.");

            if (Count > 0)
            {
                Lower = leafInfos.First().Version;
                Upper = leafInfos.Last().Version;
            }
            else
            {
                Lower = null;
                Upper = null;
            }
        }

        public async Task<RegistrationPage> GetPageAsync()
        {
            await _lazyInitializeTask.Value;
            return _page;
        }

        public async Task<IReadOnlyList<LeafInfo>> GetLeafInfosAsync()
        {
            return await GetMutableLeafInfosAsync();
        }

        private async Task<List<LeafInfo>> GetMutableLeafInfosAsync()
        {
            await _lazyInitializeTask.Value;
            return _leafInfos;
        }
    }
}

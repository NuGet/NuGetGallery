// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageListViewModel
    {
        public PackageListViewModel(
            IQueryable<Package> packages,
            User currentUser,
            DateTime? indexTimestampUtc,
            string searchTerm,
            int totalCount,
            int pageIndex,
            int pageSize,
            UrlHelper url,
            bool includePrerelease,
            bool isPreviewSearch)
        {
            // TODO: Implement actual sorting
            IEnumerable<ListPackageItemViewModel> items = packages.ToList().Select(pv => new ListPackageItemViewModel().Setup(pv, currentUser));
            PageIndex = pageIndex;
            IndexTimestampUtc = indexTimestampUtc;
            PageSize = pageSize;
            TotalCount = totalCount;
            SearchTerm = searchTerm;
            int pageCount = (TotalCount + PageSize - 1) / PageSize;

            var pager = new PreviousNextPagerViewModel<ListPackageItemViewModel>(
                items,
                PageIndex,
                pageCount,
                page => url.PackageList(page, searchTerm, includePrerelease));
            Items = pager.Items;
            FirstResultIndex = 1 + (PageIndex * PageSize);
            LastResultIndex = FirstResultIndex + Items.Count() - 1;
            Pager = pager;
            IncludePrerelease = includePrerelease;
            IsPreviewSearch = isPreviewSearch;
        }

        public int FirstResultIndex { get; }

        public IEnumerable<ListPackageItemViewModel> Items { get; }

        public int LastResultIndex { get; }

        public IPreviousNextPager Pager { get; }

        public int TotalCount { get; }

        public string SearchTerm { get;  }

        public int PageIndex { get; }

        public int PageSize { get; }

        public DateTime? IndexTimestampUtc { get; }

        public bool IncludePrerelease { get; }

        public bool IsPreviewSearch { get; }
    }
}
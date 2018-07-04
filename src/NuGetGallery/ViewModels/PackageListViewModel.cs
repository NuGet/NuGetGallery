// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

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
            bool includePrerelease)
            : this(packages, currentUser, indexTimestampUtc, searchTerm, totalCount, pageIndex, pageSize, url, curatedFeed: null, includePrerelease: includePrerelease)
        {
        }

        public PackageListViewModel(
            IQueryable<Package> packages,
            User currentUser,
            DateTime? indexTimestampUtc,
            string searchTerm,
            int totalCount,
            int pageIndex,
            int pageSize,
            UrlHelper url,
            string curatedFeed,
            bool includePrerelease)
        {
            PageIndex = pageIndex;
            IndexTimestampUtc = indexTimestampUtc;
            PageSize = pageSize;
            TotalCount = totalCount;
            SearchTerm = searchTerm;
            int pageCount = (TotalCount + PageSize - 1) / PageSize;

            var pager = new PreviousNextPagerViewModel(
                PageIndex,
                pageCount,
                page => curatedFeed == null ?
                    url.PackageList(page, searchTerm, includePrerelease) :
                    url.CuratedPackageList(page, searchTerm, curatedFeed)
                );
            Items = packages.ToList().Select(pv => new ListPackageItemViewModel(pv, currentUser)); ;
            FirstResultIndex = 1 + (PageIndex * PageSize);
            LastResultIndex = FirstResultIndex + Items.Count() - 1;
            Pager = pager;
            IncludePrerelease = includePrerelease;
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
    }
}
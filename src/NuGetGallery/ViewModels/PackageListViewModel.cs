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
            DateTime? indexTimestampUtc,
            string searchTerm,
            int totalCount,
            int pageIndex,
            int pageSize,
            UrlHelper url)
            : this(packages, indexTimestampUtc, searchTerm, totalCount, pageIndex, pageSize, url, curatedFeed: null) { }

        public PackageListViewModel(
            IQueryable<Package> packages,
            DateTime? indexTimestampUtc,
            string searchTerm,
            int totalCount,
            int pageIndex,
            int pageSize,
            UrlHelper url,
            string curatedFeed)
        {
            // TODO: Implement actual sorting
            IEnumerable<ListPackageItemViewModel> items = packages.ToList().Select(pv => new ListPackageItemViewModel(pv));
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
                page => curatedFeed == null ?
                    url.PackageList(page, searchTerm) :
                    url.CuratedPackageList(page, searchTerm, curatedFeed)
                );
            Items = pager.Items;
            FirstResultIndex = 1 + (PageIndex * PageSize);
            LastResultIndex = FirstResultIndex + Items.Count() - 1;
            Pager = pager;
        }

        public int FirstResultIndex { get; set; }

        public IEnumerable<ListPackageItemViewModel> Items { get; private set; }

        public int LastResultIndex { get; set; }

        public IPreviousNextPager Pager { get; private set; }

        public int TotalCount { get; private set; }

        public string SearchTerm { get; private set; }

        public int PageIndex { get; private set; }

        public int PageSize { get; private set; }

        public DateTime? IndexTimestampUtc { get; private set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using MvcMiniProfiler;

namespace NuGetGallery {
    public class PackageListViewModel {
        public PackageListViewModel(IQueryable<Package> packages,
            string searchTerm,
            string sortOrder,
            int pageIndex,
            int pageSize,
            UrlHelper url) {
            // TODO: Implement actual sorting
            IEnumerable<ListPackageItemViewModel> items;
            using (MiniProfiler.Current.Step("Querying and mapping packages to list")) {
                items = packages.OrderBy(p => p.PackageRegistration.DownloadCount)
                                    .Skip(pageIndex * pageSize)
                                    .Take(pageSize)
                                    .ToList()
                                    .Select(pv => new ListPackageItemViewModel(pv));
            }
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalCount = packages.Count();
            SortOrder = sortOrder;
            SearchTerm = searchTerm;
            int pageCount = (TotalCount + PageSize - 1) / PageSize;

            var pager = new PreviousNextPagerViewModel<ListPackageItemViewModel>(
                items,
                PageIndex,
                PageSize,
                pageCount,
                page => url.PackageList(page, sortOrder, searchTerm)
            );
            Items = pager.Items;
            Pager = pager;
        }

        public IEnumerable<ListPackageItemViewModel> Items { get; private set; }

        public IPreviousNextPager Pager { get; private set; }

        public int TotalCount { get; private set; }

        public string SearchTerm { get; private set; }

        public string SortOrder { get; private set; }

        public int PageIndex { get; private set; }

        public int PageSize { get; private set; }
    }
}
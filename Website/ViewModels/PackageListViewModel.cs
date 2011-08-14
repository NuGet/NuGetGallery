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
            IEnumerable<DisplayPackageViewModel> items;
            using (MiniProfiler.Current.Step("Querying and mapping packages to list")) {
                items = packages.OrderBy(p => p.PackageRegistration.DownloadCount)
                                    .Skip(pageIndex * pageSize)
                                    .Take(pageSize)
                                    .ToList()
                                    .Select(pv => new DisplayPackageViewModel(pv));
            }
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalCount = packages.Count();
            SortOrder = sortOrder;
            SearchTerm = searchTerm;
            var pager = new PreviousNextPagerViewModel<DisplayPackageViewModel>(
                items,
                PageIndex,
                PageSize,
                page => url.PackageList(page, sortOrder, searchTerm)
            );
            Items = pager.Items;
            Pager = pager;
        }

        public IEnumerable<DisplayPackageViewModel> Items { get; private set; }

        public IPreviousNextPager Pager { get; private set; }

        public int TotalCount { get; private set; }

        public string SearchTerm { get; private set; }

        public string SortOrder { get; private set; }

        public int PageIndex { get; private set; }

        public int PageSize { get; private set; }
    }
}
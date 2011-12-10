﻿using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using MvcMiniProfiler;

namespace NuGetGallery
{
    public class PackageListViewModel
    {
        public PackageListViewModel(IQueryable<Package> packages,
            string searchTerm,
            string sortOrder,
            int pageIndex,
            int pageSize,
            UrlHelper url)
        {
            // TODO: Implement actual sorting
            IEnumerable<ListPackageItemViewModel> items;
            using (MiniProfiler.Current.Step("Querying and mapping packages to list"))
            {
                items = packages.SortBy(GetSortExpression(sortOrder))
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

        public string SortOrder { get; private set; }

        public int PageIndex { get; private set; }

        public int PageSize { get; private set; }

        private string GetSortExpression(string sortOrder)
        {
            switch (sortOrder)
            {
                case "package-title":
                    return "PackageRegistration.Id";
                case "package-created":
                    return "Published desc";
            }
            return "PackageRegistration.DownloadCount desc";
        }
    }
}
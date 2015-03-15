using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// View model for displaying all search results for packages to import.
    /// </summary>
    public class ImportSearchViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportSearchViewModel"/> class.
        /// </summary>
        /// <param name="packages">The packages.</param>
        /// <param name="searchTerm">The search term.</param>
        /// <param name="sortOrder">The sort order.</param>
        /// <param name="totalCount">The total count.</param>
        /// <param name="pageIndex">Index of the page.</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <param name="url">The URL.</param>
        /// <param name="includePrerelease">if set to <c>true</c> include prerelease.</param>
        public ImportSearchViewModel(
            IQueryable<NuGetService.V2FeedPackage> packages,
            string searchTerm,
            string sortOrder,
            int totalCount,
            int pageIndex,
            int pageSize,
            UrlHelper url,
            bool includePrerelease)
        {
            // TODO: Implement actual sorting
            IEnumerable<ImportSearchItemViewModel> items = 
                packages.ToList()
                        .Select(pv => new ImportSearchItemViewModel(pv));
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalCount = totalCount;
            SortOrder = sortOrder;
            SearchTerm = searchTerm;
            int pageCount = (TotalCount + PageSize - 1) / PageSize;

            var pager = new PreviousNextPagerViewModel<ImportSearchItemViewModel>(
                items,
                PageIndex,
                pageCount,
                page => url.RouteUrl("Admin_import", new { page, sortOrder, q = searchTerm, includePrerelease })
                );
            Items = pager.Items;
            FirstResultIndex = 1 + (PageIndex * PageSize);
            LastResultIndex = FirstResultIndex + Items.Count() - 1;
            Pager = pager;
            IncludePrerelease = includePrerelease ? "true" : null;
        }

        public int FirstResultIndex { get; set; }

        public IEnumerable<ImportSearchItemViewModel> Items { get; private set; }

        public int LastResultIndex { get; set; }

        public IPreviousNextPager Pager { get; private set; }

        public int TotalCount { get; private set; }

        public string SearchTerm { get; private set; }

        public string SortOrder { get; private set; }

        public int PageIndex { get; private set; }

        public int PageSize { get; private set; }

        public string IncludePrerelease { get; private set; }
    }
}
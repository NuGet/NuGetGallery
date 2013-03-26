using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class PreviousNextPagerViewModel<T> : IPreviousNextPager
    {
        public PreviousNextPagerViewModel(
            IList<T> items,
            int pageIndex,
            int totalPages,
            Func<int, string> url)
        {
            int pageNumber = pageIndex == Int32.MaxValue ? pageIndex : pageIndex + 1;
            Items = items;
            HasPreviousPage = pageNumber > 1;
            HasNextPage = pageNumber < totalPages;
            NextPageUrl = url(pageNumber + 1);
            PreviousPageUrl = url(pageNumber - 1);
        }

        public IList<T> Items { get; private set; }

        public bool HasNextPage { get; private set; }
        public bool HasPreviousPage { get; private set; }
        public string NextPageUrl { get; private set; }
        public string PreviousPageUrl { get; private set; }
    }
}
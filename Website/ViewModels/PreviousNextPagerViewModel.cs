using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery {
    public class PreviousNextPagerViewModel<T> : IPreviousNextPager {

        public PreviousNextPagerViewModel(IEnumerable<T> items,
            int pageIndex,
            int pageSize,
            Func<int, string> url) {

            HasPreviousPage = pageIndex > 0;
            Items = items.Skip(pageIndex * pageSize).Take(pageSize);
            HasNextPage = items.Skip((pageIndex + 1) * pageSize).Any();
            NextPageUrl = url(pageIndex + 2);
            PreviousPageUrl = url(pageIndex);
        }

        public bool HasNextPage { get; private set; }
        public bool HasPreviousPage { get; private set; }
        public string NextPageUrl { get; private set; }
        public string PreviousPageUrl { get; private set; }
        public IEnumerable<T> Items { get; private set; }
    }
}
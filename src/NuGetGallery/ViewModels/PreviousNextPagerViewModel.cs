// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class PreviousNextPagerViewModel<T> : IPreviousNextPager
    {
        public PreviousNextPagerViewModel(
            IReadOnlyCollection<T> items,
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

        public IReadOnlyCollection<T> Items { get; private set; }

        public bool HasNextPage { get; }
        public bool HasPreviousPage { get; }
        public string NextPageUrl { get; }
        public string PreviousPageUrl { get; }
    }
}
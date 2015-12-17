// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGetGallery
{
    public class SearchResults
    {
        public int Hits { get; private set; }
        public DateTime? IndexTimestampUtc { get; private set; }
        public IQueryable<Package> Data { get; private set; }

        public SearchResults(int hits, DateTime? indexTimestampUtc)
            : this(hits, indexTimestampUtc, Enumerable.Empty<Package>().AsQueryable())
        {
        }

        public SearchResults(int hits, DateTime? indexTimestampUtc, IQueryable<Package> data)
        {
            Hits = hits;
            Data = data;
            IndexTimestampUtc = indexTimestampUtc;
        }
    }
}
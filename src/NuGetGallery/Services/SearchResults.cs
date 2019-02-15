// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class SearchResults
    {
        public int Hits { get; private set; }
        public DateTime? IndexTimestampUtc { get; private set; }
        public IQueryable<Package> Data { get; private set; }

        /// <summary>
        /// Indicates that the Search was successful and the results are complete.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        public SearchResults(int hits, DateTime? indexTimestampUtc)
            : this(hits, indexTimestampUtc, Enumerable.Empty<Package>().AsQueryable())
        {
        }

        public SearchResults(int hits, DateTime? indexTimestampUtc, IQueryable<Package> data)
             : this(hits, indexTimestampUtc, data, HttpStatusCode.OK)
        {
        }

        public SearchResults(int hits, DateTime? indexTimestampUtc, IQueryable<Package> data, HttpStatusCode statusCode)
        {
            Hits = hits;
            Data = data;
            IndexTimestampUtc = indexTimestampUtc;
            StatusCode = statusCode;
        }

        public static bool IsSuccessful(SearchResults searchResults)
        {
            return (int)searchResults.StatusCode >= 200 && (int)searchResults.StatusCode <= 299;
        }
    }
}
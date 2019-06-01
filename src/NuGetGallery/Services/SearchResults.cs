// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class SearchResults
    {
        public int Hits { get; private set; }
        public DateTime? IndexTimestampUtc { get; }
        public IQueryable<Package> Data { get; }

        /// <summary>
        /// The response message.
        /// </summary>
        public HttpResponseMessage ResponseMessage { get; }

        public SearchResults(int hits, DateTime? indexTimestampUtc)
            : this(hits, indexTimestampUtc, Enumerable.Empty<Package>().AsQueryable())
        {
        }

        public SearchResults(int hits, DateTime? indexTimestampUtc, IQueryable<Package> data)
             : this(hits, indexTimestampUtc, data, null)
        {
        }

        public SearchResults(int hits, DateTime? indexTimestampUtc, IQueryable<Package> data, HttpResponseMessage responseMessage)
        {
            Hits = hits;
            Data = data;
            IndexTimestampUtc = indexTimestampUtc;
            ResponseMessage = responseMessage;
        }

        public static bool IsSuccessful(SearchResults searchResults)
        {
            return searchResults.ResponseMessage?.IsSuccessStatusCode ?? true;
        }
    }
}
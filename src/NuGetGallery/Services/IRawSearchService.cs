// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IRawSearchService
    {
        /// <summary>
        /// Executes a raw lucene query against the search index
        /// </summary>
        /// <param name="filter">The query to execute, with the search term interpreted as a raw lucene query</param>
        /// <returns>The results of the query</returns>
        Task<SearchResults> RawSearch(SearchFilter filter);
    }
}
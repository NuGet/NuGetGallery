// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ISearchService
    {
        /// <summary>
        ///     Searches for packages that match the search filter and returns a set of results.
        /// </summary>
        /// <param name="filter"> The filter to be used. </param>
        /// <returns>The number of hits in the search and, if the CountOnly flag in SearchFilter was false, the results themselves</returns>
        Task<SearchResults> Search(SearchFilter filter);

        /// <summary>
        /// Gets a boolean indicating if all versions of each package are stored in the index
        /// </summary>
        bool ContainsAllVersions { get; }

        /// <summary>
        /// Gets a boolean indicating if the search service supports Advanced Search.
        /// </summary>
        bool SupportsAdvancedSearch { get; }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.SearchService
{
    public interface ISearchService
    {
        /// <summary>
        /// Perform a V2 search query.
        /// </summary>
        /// <param name="request">The V2 search request.</param>
        /// <returns>The V2 search response.</returns>
        /// <exception cref="InvalidSearchRequestException">Thrown if the request is invalid.</exception>
        Task<V2SearchResponse> V2SearchAsync(V2SearchRequest request);

        /// <summary>
        /// Perform a V3 search query.
        /// </summary>
        /// <param name="request">The V3 search request.</param>
        /// <returns>The V3 search response.</returns>
        /// <exception cref="InvalidSearchRequestException">Thrown if the request is invalid.</exception>
        Task<V3SearchResponse> V3SearchAsync(V3SearchRequest request);

        /// <summary>
        /// Perform an autocomplete query.
        /// </summary>
        /// <param name="request">The autocomplete request.</param>
        /// <returns>The autocomplete response.</returns>
        /// <exception cref="InvalidSearchRequestException">Thrown if the request is invalid.</exception>
        Task<AutocompleteResponse> AutocompleteAsync(AutocompleteRequest request);
    }
}
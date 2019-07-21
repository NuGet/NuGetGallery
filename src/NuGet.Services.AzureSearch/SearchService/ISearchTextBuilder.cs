// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Generates Azure Search Query using Lucene query syntax.
    /// See: https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax
    /// </summary>
    public interface ISearchTextBuilder
    {
        /// <summary>
        /// Map a V2 search request to Azure Search.
        /// </summary>
        /// <param name="request">The V2 search request.</param>
        /// <returns>The Azure Search query.</returns>
        /// <exception cref="InvalidSearchRequestException">Thrown on invalid search requests.</exception>
        ParsedQuery ParseV2Search(V2SearchRequest request);

        /// <summary>
        /// Map a V3 search request to Azure Search.
        /// </summary>
        /// <param name="request">The V3 search request.</param>
        /// <returns>The Azure Search query.</returns>
        /// <exception cref="InvalidSearchRequestException">Thrown on invalid search requests.</exception>
        ParsedQuery ParseV3Search(V3SearchRequest request);

        /// <summary>
        /// Build a parsed query into search text.
        /// </summary>
        /// <param name="parsed">The parsed query.</param>
        /// <returns>The Lucene search text to pass to Azure Search.</returns>
        string Build(ParsedQuery parsed);

        /// <summary>
        /// Map an autocomplete request to Azure Search.
        /// </summary>
        /// <param name="request">The autocomplete request.</param>
        /// <returns>The Azure Search query.</returns>
        /// <exception cref="InvalidSearchRequestException">Thrown on invalid autocomplete requests.</exception>
        string Autocomplete(AutocompleteRequest request);
    }
}
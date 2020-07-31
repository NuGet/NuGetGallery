// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Builds a set of index actions for a specific package ID against the search index. No hijack index actions
    /// should be returned by this interface.
    /// </summary>
    public interface ISearchIndexActionBuilder
    {
        /// <summary>
        /// Generates a set of index actions for all search documents that exist for this package ID. The document
        /// for each search filter that is sent to the search index is built by <paramref name="buildDocument"/>. It is
        /// assumed that the caller's implementation of the delegate knows the <paramref name="packageId"/> by context.
        /// </summary>
        /// <param name="packageId">The package ID to produce documents for.</param>
        /// <param name="buildDocument">A delegate used to initialize the document.</param>
        /// <returns>The index actions.</returns>
        Task<IndexActions> UpdateAsync(string packageId, Func<SearchFilters, KeyedDocument> buildDocument);
    }
}
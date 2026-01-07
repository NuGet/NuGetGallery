// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// This interface encapsulates the selection of Azure Search operation as well as the generation of parameters for
    /// the selected operation. Query optimizations such as looking up a document by key instead of doing a full Lucene
    /// query are decided here.
    /// </summary>
    public interface IIndexOperationBuilder
    {
        IndexOperation Autocomplete(AutocompleteRequest request);
        IndexOperation V2SearchWithHijackIndex(V2SearchRequest request);
        IndexOperation V2SearchWithSearchIndex(V2SearchRequest request);
        IndexOperation V3Search(V3SearchRequest request);
    }
}
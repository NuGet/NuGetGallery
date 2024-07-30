// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    public enum IndexOperationType
    {
        /// <summary>
        /// The data for the user was fetched using Azure Search's "get document by key" API. The .NET API is called "Get" and
        /// "GetAsync" but REST API is called "lookup".
        /// https://docs.microsoft.com/en-us/rest/api/searchservice/lookup-document
        /// </summary>
        Get,

        /// <summary>
        /// The data for the user was fetched using Azure Search's "search documents" API.
        /// https://docs.microsoft.com/en-us/rest/api/searchservice/search-documents
        /// </summary>
        Search,

        /// <summary>
        /// The request should yield an empty response so no Azure Search query is necessary.
        /// </summary>
        Empty,
    }
}

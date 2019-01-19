// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchTextBuilder : ISearchTextBuilder
    {
        public string V2Search(V2SearchRequest request)
        {
            var query = request.Query;

            if (request.LuceneQuery)
            {
                // TODO: convert a leading "id:" to "packageid:"
                // https://github.com/NuGet/NuGetGallery/issues/6456
            }

            return GetLuceneQuery(query);
        }

        public string V3Search(V3SearchRequest request)
        {
            return GetLuceneQuery(request.Query);
        }

        private static string GetLuceneQuery(string query)
        {
            // TODO: query parsing
            // https://github.com/NuGet/NuGetGallery/issues/6456
            return query ?? "*";
        }
    }
}

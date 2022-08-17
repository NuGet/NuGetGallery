// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Search.Documents.Models;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class SingleSearchResultPage<T>
    {
        public SingleSearchResultPage(IReadOnlyList<SearchResult<T>> values, long? count)
        {
            Values = values;
            Count = count;
        }

        public SingleSearchResultPage(IReadOnlyList<SearchResult<T>> values, long? count, IDictionary<string, IList<FacetResult>> facets)
        {
            Values = values;
            Count = count;
            Facets = facets;
        }

        public IReadOnlyList<SearchResult<T>> Values { get; set; }
        public long? Count { get; }
        public IDictionary<string, IList<FacetResult>> Facets { get; set; }

    }
}

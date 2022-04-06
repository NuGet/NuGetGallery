// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Azure.Search.Documents.Models;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class SingleSearchResultPage<T>
    {
        public SingleSearchResultPage(IReadOnlyList<SearchResult<T>> values, long? count)
        {
            Values = values;
            Count = count;
        }

        public IReadOnlyList<SearchResult<T>> Values { get; set; }
        public long? Count { get; }
    }
}

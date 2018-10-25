// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Search;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class SearchServiceClientWrapper : ISearchServiceClientWrapper
    {
        private readonly ISearchServiceClient _inner;

        public SearchServiceClientWrapper(ISearchServiceClient inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Indexes = new IndexesOperationsWrapper(_inner.Indexes);
        }

        public IIndexesOperationsWrapper Indexes { get; }
    }
}

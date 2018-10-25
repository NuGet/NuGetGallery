// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Search;

namespace NuGet.Services.AzureSearch.Wrappers
{
    public class SearchIndexClientWrapper : ISearchIndexClientWrapper
    {
        private readonly ISearchIndexClient _inner;

        public SearchIndexClientWrapper(ISearchIndexClient inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Documents = new DocumentsOperationsWrapper(_inner.Documents);
        }

        public string IndexName => _inner.IndexName;
        public IDocumentsOperationsWrapper Documents { get; }
    }
}

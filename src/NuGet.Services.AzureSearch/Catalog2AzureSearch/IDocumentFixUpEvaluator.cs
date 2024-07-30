// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public interface IDocumentFixUpEvaluator
    {
        Task<DocumentFixUp> TryFixUpAsync(
            IReadOnlyList<CatalogCommitItem> itemList,
            ConcurrentBag<IdAndValue<IndexActions>> allIndexActions,
            InvalidOperationException exception);
    }
}
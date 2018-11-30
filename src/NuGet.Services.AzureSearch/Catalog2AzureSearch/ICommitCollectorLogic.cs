// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public interface ICommitCollectorLogic
    {
        Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(IEnumerable<CatalogCommitItem> catalogItems);
        Task OnProcessBatchAsync(IEnumerable<CatalogCommitItem> items);
    }
}
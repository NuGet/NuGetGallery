// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public interface ICatalogIndexActionBuilder
    {
        Task<IndexActions> AddCatalogEntriesAsync(
            string packageId,
            IReadOnlyList<CatalogCommitItem> latestEntries,
            IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> entryToLeaf);
    }
}
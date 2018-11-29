// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;

namespace NuGet.Services.AzureSearch
{
    public interface ISearchDocumentBuilder
    {
        KeyedDocument Keyed(
            string packageId,
            SearchFilters searchFilters);

        SearchDocument.UpdateVersionList UpdateVersionList(
            string packageId,
            SearchFilters searchFilters,
            string[] versions);

        SearchDocument.Full Full(
            string packageId, 
            SearchFilters searchFilters,
            string[] versions,
            string fullVersion,
            Package package,
            string[] owners,
            long totalDownloadCount);

        SearchDocument.UpdateLatest UpdateLatest(
            SearchFilters searchFilters,
            string[] versions,
            string normalizedVersion,
            string fullVersion,
            PackageDetailsCatalogLeaf leaf);
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;

namespace NuGet.Services.AzureSearch
{
    public interface ISearchDocumentBuilder
    {
        SearchDocument.LatestFlags LatestFlagsOrNull(
            VersionLists versionLists,
            SearchFilters searchFilters);

        KeyedDocument Keyed(
            string packageId,
            SearchFilters searchFilters);

        SearchDocument.UpdateOwners UpdateOwners(
            string packageId,
            SearchFilters searchFilters,
            string[] owners);

        SearchDocument.UpdateDownloadCount UpdateDownloadCount(
            string packageId,
            SearchFilters searchFilters,
            long totalDownloadCount);

        SearchDocument.UpdateVersionList UpdateVersionListFromCatalog(
            string packageId,
            SearchFilters searchFilters,
            DateTimeOffset lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest);

        SearchDocument.UpdateVersionListAndOwners UpdateVersionListAndOwnersFromCatalog(
            string packageId,
            SearchFilters searchFilters,
            DateTimeOffset lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string[] owners);

        SearchDocument.Full FullFromDb(
            string packageId, 
            SearchFilters searchFilters,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string fullVersion,
            Package package,
            string[] owners,
            long totalDownloadCount);

        SearchDocument.UpdateLatest UpdateLatestFromCatalog(
            SearchFilters searchFilters,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string normalizedVersion,
            string fullVersion,
            PackageDetailsCatalogLeaf leaf,
            string[] owners);
    }
}
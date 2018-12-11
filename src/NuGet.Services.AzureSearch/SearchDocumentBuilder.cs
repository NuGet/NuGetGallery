// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;

namespace NuGet.Services.AzureSearch
{
    public class SearchDocumentBuilder : ISearchDocumentBuilder
    {
        public KeyedDocument Keyed(
            string packageId,
            SearchFilters searchFilters)
        {
            var document = new KeyedDocument();

            PopulateKey(document, packageId, searchFilters);

            return document;
        }

        public SearchDocument.UpdateVersionList UpdateVersionList(
            string packageId,
            SearchFilters searchFilters,
            string[] versions)
        {
            var document = new SearchDocument.UpdateVersionList();

            PopulateVersions(document, packageId, searchFilters, versions);

            return document;
        }

        public SearchDocument.UpdateLatest UpdateLatest(
            SearchFilters searchFilters,
            string[] versions,
            string normalizedVersion,
            string fullVersion,
            PackageDetailsCatalogLeaf leaf)
        {
            var document = new SearchDocument.UpdateLatest();

            PopulateUpdateLatest(document, leaf.PackageId, searchFilters, versions, fullVersion);
            DocumentUtilities.PopulateMetadata(document, normalizedVersion, leaf);

            return document;
        }

        public SearchDocument.Full Full(
            string packageId,
            SearchFilters searchFilters,
            string[] versions,
            string fullVersion,
            Package package,
            string[] owners,
            long totalDownloadCount)
        {
            var document = new SearchDocument.Full();

            PopulateAddFirst(document, packageId, searchFilters, versions, fullVersion, owners);
            DocumentUtilities.PopulateMetadata(document, packageId, package);
            document.TotalDownloadCount = totalDownloadCount;

            return document;
        }

        private static void PopulateVersions<T>(
            T document,
            string packageId,
            SearchFilters searchFilters,
            string[] versions) where T : KeyedDocument, SearchDocument.IVersions
        {
            PopulateKey(document, packageId, searchFilters);
            document.Versions = versions;
        }

        private static void PopulateKey(KeyedDocument document, string packageId, SearchFilters searchFilters)
        {
            document.Key = DocumentUtilities.GetSearchDocumentKey(packageId, searchFilters);
        }

        private static void PopulateUpdateLatest(
            SearchDocument.UpdateLatest document,
            string packageId,
            SearchFilters searchFilters,
            string[] versions,
            string fullVersion)
        {
            PopulateVersions(document, packageId, searchFilters, versions);
            document.SearchFilters = searchFilters.ToString();
            document.FullVersion = fullVersion;
        }

        private static void PopulateAddFirst(
            SearchDocument.AddFirst document,
            string packageId,
            SearchFilters searchFilters,
            string[] versions,
            string fullVersion,
            string[] owners)
        {
            PopulateUpdateLatest(document, packageId, searchFilters, versions, fullVersion);
            document.Owners = owners;
        }
    }
}

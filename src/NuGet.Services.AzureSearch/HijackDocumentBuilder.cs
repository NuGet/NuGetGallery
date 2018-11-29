// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;

namespace NuGet.Services.AzureSearch
{
    public class HijackDocumentBuilder : IHijackDocumentBuilder
    {
        public KeyedDocument Keyed(
            string packageId,
            string normalizedVersion)
        {
            var document = new KeyedDocument();

            PopulateKey(document, packageId, normalizedVersion);

            return document;
        }

        public HijackDocument.Latest Latest(
            string packageId,
            string normalizedVersion,
            HijackDocumentChanges changes)
        {
            var document = new HijackDocument.Latest();

            PopulateLatest(document, packageId, normalizedVersion, changes);

            return document;
        }

        public HijackDocument.Full Full(
            string normalizedVersion,
            HijackDocumentChanges changes,
            PackageDetailsCatalogLeaf leaf)
        {
            var document = new HijackDocument.Full();

            PopulateFull(document, leaf.PackageId, normalizedVersion, changes, leaf.Title);
            DocumentUtilities.PopulateMetadata(document, normalizedVersion, leaf);

            return document;
        }

        public HijackDocument.Full Full(
            string packageId,
            HijackDocumentChanges changes,
            Package package)
        {
            var document = new HijackDocument.Full();

            PopulateFull(document, packageId, package.NormalizedVersion, changes, package.Title);
            DocumentUtilities.PopulateMetadata(document, packageId, package);

            return document;
        }

        private static void PopulateFull(
            HijackDocument.Full document,
            string packageId,
            string normalizedVersion,
            HijackDocumentChanges changes,
            string title)
        {
            PopulateLatest(document, packageId, normalizedVersion, changes);
            document.SortableTitle = title ?? packageId;
        }

        private static void PopulateLatest<T>(
            T document,
            string packageId,
            string normalizedVersion,
            HijackDocumentChanges changes) where T : KeyedDocument, HijackDocument.ILatest
        {
            PopulateKey(document, packageId, normalizedVersion);
            document.IsLatestStableSemVer1 = changes.LatestStableSemVer1;
            document.IsLatestSemVer1 = changes.LatestSemVer1;
            document.IsLatestStableSemVer2 = changes.LatestStableSemVer2;
            document.IsLatestSemVer2 = changes.LatestSemVer2;
        }

        private static void PopulateKey(KeyedDocument document, string packageId, string normalizedVersion)
        {
            document.Key = DocumentUtilities.GetHijackDocumentKey(packageId, normalizedVersion);
        }
    }
}

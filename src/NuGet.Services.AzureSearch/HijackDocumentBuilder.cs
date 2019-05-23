// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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

        public HijackDocument.Latest LatestFromCatalog(
            string packageId,
            string normalizedVersion,
            DateTimeOffset lastCommitTimestamp,
            string lastCommitId,
            HijackDocumentChanges changes)
        {
            var document = new HijackDocument.Latest();

            PopulateLatest(
                document,
                packageId,
                normalizedVersion,
                lastUpdatedFromCatalog: true,
                lastCommitTimestamp: lastCommitTimestamp,
                lastCommitId: lastCommitId,
                changes: changes);

            return document;
        }

        public HijackDocument.Full FullFromCatalog(
            string normalizedVersion,
            HijackDocumentChanges changes,
            PackageDetailsCatalogLeaf leaf)
        {
            var document = new HijackDocument.Full();

            PopulateLatest(
                document,
                leaf.PackageId,
                normalizedVersion,
                lastUpdatedFromCatalog: true,
                lastCommitTimestamp: leaf.CommitTimestamp,
                lastCommitId: leaf.CommitId,
                changes: changes);
            DocumentUtilities.PopulateMetadata(document, normalizedVersion, leaf);
            document.Listed = leaf.IsListed();

            return document;
        }

        public HijackDocument.Full FullFromDb(
            string packageId,
            HijackDocumentChanges changes,
            Package package)
        {
            var document = new HijackDocument.Full();

            PopulateLatest(
                document,
                packageId,
                lastUpdatedFromCatalog: false,
                lastCommitTimestamp: null,
                lastCommitId: null,
                normalizedVersion: package.NormalizedVersion,
                changes: changes);
            DocumentUtilities.PopulateMetadata(document, packageId, package);
            document.Listed = package.Listed;

            return document;
        }

        private static void PopulateLatest<T>(
            T document,
            string packageId,
            string normalizedVersion,
            bool lastUpdatedFromCatalog,
            DateTimeOffset? lastCommitTimestamp,
            string lastCommitId,
            HijackDocumentChanges changes) where T : KeyedDocument, HijackDocument.ILatest
        {
            PopulateKey(document, packageId, normalizedVersion);
            DocumentUtilities.PopulateCommitted(
                document,
                lastUpdatedFromCatalog,
                lastCommitTimestamp,
                lastCommitId);
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

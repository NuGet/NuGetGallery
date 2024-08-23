// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;

namespace NuGet.Services.AzureSearch
{
    public interface IHijackDocumentBuilder
    {
        KeyedDocument Keyed(
            string packageId,
            string normalizedVersion);

        HijackDocument.Latest LatestFromCatalog(
            string packageId,
            string normalizedVersion,
            DateTimeOffset lastCommitTimestamp,
            string lastCommitId,
            HijackDocumentChanges changes);

        HijackDocument.Full FullFromDb(
            string packageId,
            HijackDocumentChanges changes,
            Package package);

        HijackDocument.Full FullFromCatalog(
            string normalizedVersion,
            HijackDocumentChanges changes,
            PackageDetailsCatalogLeaf leaf);
    }
}
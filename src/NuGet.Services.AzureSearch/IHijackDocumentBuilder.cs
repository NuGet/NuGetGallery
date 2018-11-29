// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;

namespace NuGet.Services.AzureSearch
{
    public interface IHijackDocumentBuilder
    {
        KeyedDocument Keyed(
            string packageId,
            string normalizedVersion);

        HijackDocument.Latest Latest(
            string packageId,
            string normalizedVersion,
            HijackDocumentChanges changes);

        HijackDocument.Full Full(
            string packageId,
            HijackDocumentChanges changes,
            Package package);

        HijackDocument.Full Full(
            string normalizedVersion,
            HijackDocumentChanges changes,
            PackageDetailsCatalogLeaf leaf);
    }
}
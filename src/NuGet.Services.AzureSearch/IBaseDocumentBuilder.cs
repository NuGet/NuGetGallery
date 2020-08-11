// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;

namespace NuGet.Services.AzureSearch
{
    public interface IBaseDocumentBuilder
    {
        void PopulateUpdated(
            IUpdatedDocument document,
            bool lastUpdatedFromCatalog);
        void PopulateCommitted(
            ICommittedDocument document,
            bool lastUpdatedFromCatalog,
            DateTimeOffset? lastCommitTimestamp,
            string lastCommitId);
        void PopulateMetadata(
            IBaseMetadataDocument document,
            string packageId,
            Package package);
        void PopulateMetadata(
            IBaseMetadataDocument document,
            string normalizedVersion,
            PackageDetailsCatalogLeaf leaf);
    }
}
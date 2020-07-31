// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Jobs.Catalog2Registration
{
    public interface IHiveMerger
    {
        /// <summary>
        /// Merge the incoming list of catalog items into the registration index. This method will not go into the
        /// details of updating the leaf item metadata. That responsiblity is left up to the caller who can inspect the
        /// <see cref="HiveMergeResult.ModifiedLeaves"/>. This logic also does not handle the inlining or externalizing
        /// of leaf items since this is more of a storage concern. The provided <see cref="IndexInfo"/> bookkeeping
        /// object and its child object will be modified and the changes will be detailed in the returned
        /// <see cref="HiveMergeResult"/>.
        /// </summary>
        Task<HiveMergeResult> MergeAsync(IndexInfo indexInfo, IReadOnlyList<CatalogCommitItem> sortedCatalog);
    }
}

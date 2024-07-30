// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public interface ICatalogLeafFetcher
    {
        /// <summary>
        /// Fetch information about the latest versions available in the catalog, via the package metadata
        /// (registration) resource. At least one version in each provided version list is returned in the
        /// <see cref="LatestCatalogLeaves.Available"/> property of the result, assuming there is are
        /// any available versions. Versions referenced in the input version lists that could have been the new latest
        /// version but are deleted will appear in <see cref="LatestCatalogLeaves.Unavailable"/>. Versions not
        /// referenced in the version lists but available in the registration index will be ignored.
        /// 
        /// The whole purpose of this class is to handle the <see cref="SearchIndexChangeType.DowngradeLatest"/> case
        /// where we need to put a lower version's metadata in the search index document but we don't have that metadata
        /// available. For example, this happens when a single catalog leaf comes in unlisting the currently latest
        /// version.
        /// 
        /// The input is a list of lists because multiple downgrades can happen for a single package ID. For example,
        /// consider the latest version was 3.0.0 and the other versions are 1.0.0 and 2.0.0-beta. If 3.0.0 is unlisted
        /// then 1.0.0 is the latest version for <see cref="SearchFilters.Default"/> but 2.0.0-beta is the latest
        /// version for <see cref="SearchFilters.IncludePrerelease"/>. There can be as many input version lists as there
        /// are different <see cref="SearchFilters"/> values.
        /// </summary>
        /// <param name="packageId">The package ID to fetch catalog leaves for.</param>
        /// <param name="versions">The list of candidate latest versions.</param>
        /// <returns>The latest catalog leaves.</returns>
        Task<LatestCatalogLeaves> GetLatestLeavesAsync(string packageId, IReadOnlyList<IReadOnlyList<NuGetVersion>> versions);
    }
}

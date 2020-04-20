// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Fetches auxiliary data from the Gallery database.
    /// </summary>
    public interface IDatabaseAuxiliaryDataFetcher
    {
        /// <summary>
        /// Fetch the owners for a specific package ID. If the package registration does not exist or if there are no
        /// owners, an empty string array is returned. If there are owners, they are sorted.
        /// <param name="id">The package ID to fetch owners for.</param>
        /// <returns>The sorted array of owners. Can be empty but won't ever be null.</returns>
        Task<string[]> GetOwnersOrEmptyAsync(string id);

        /// <summary>
        /// Fetch a mapping from package ID to set of owners for each package registration (i.e. package ID) in the
        /// gallery database.
        /// </summary>
        Task<SortedDictionary<string, SortedSet<string>>> GetPackageIdToOwnersAsync();

        /// <summary>
        /// Fetch a mapping of package IDs to set of replacement package IDs for each renamed packages that transfer
        /// popularity in the gallery database.
        /// </summary>
        Task<SortedDictionary<string, SortedSet<string>>> GetPackageIdToPopularityTransfersAsync();

        /// <summary>
        /// Fetch the set of all verified package IDs.
        /// </summary>
        Task<HashSet<string>> GetVerifiedPackagesAsync();
    }
}
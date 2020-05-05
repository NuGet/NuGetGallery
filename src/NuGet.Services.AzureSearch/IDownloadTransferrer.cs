// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Determines the downloads that should be changed due to popularity transfers.
    /// </summary>
    public interface IDownloadTransferrer
    {
        /// <summary>
        /// Determine changes that should be applied to the initial downloads data due to popularity transfers.
        /// </summary>
        /// <param name="downloads">The initial downloads data.</param>
        /// <param name="popularityTransfers">
        /// The initial popularity transfers. Maps packages transferring their popularity
        /// away to the list of packages receiving the popularity.
        /// </param>
        /// <param name="downloadOverrides">
        /// The initial download overrides. Maps package that should be overriden to
        /// their desired download value.
        /// </param>
        /// <returns>
        /// The downloads that are changed due to transfers. Maps package ids to the new download value.
        /// </returns>
        SortedDictionary<string, long> InitializeDownloadTransfers(
            DownloadData downloads,
            PopularityTransferData popularityTransfers,
            IReadOnlyDictionary<string, long> downloadOverrides);

        /// <summary>
        /// Determine changes that should be applied to the latest downloads data due to popularity transfers.
        /// </summary>
        /// <param name="downloads">The latest downloads data.</param>
        /// <param name="downloadChanges">The downloads that have changed since the last index.</param>
        /// <param name="oldTransfers">
        /// The previously indexed popularity transfers. Maps packages transferring their popularity
        /// away to the list of packages receiving the popularity.
        /// </param>
        /// <param name="newTransfers">
        /// The latest popularity transfers. Maps packages transferring their popularity
        /// away to the list of packages receiving the popularity.
        /// </param>
        /// <param name="downloadOverrides">
        /// The latest download overrides. Maps package that should be overriden to
        /// their desired download value.
        /// </param>
        /// <returns>
        /// The downloads that are changed due to transfers. Maps package ids to the new download value.
        /// </returns>
        SortedDictionary<string, long> UpdateDownloadTransfers(
            DownloadData downloads,
            SortedDictionary<string, long> downloadChanges,
            PopularityTransferData oldTransfers,
            PopularityTransferData newTransfers,
            IReadOnlyDictionary<string, long> downloadOverrides);
    }
}
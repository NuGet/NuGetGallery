// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    /// <summary>
    /// Used to compare two sets of data to determine the changes.
    /// </summary>
    public interface IDataSetComparer
    {
        /// <summary>
        /// Compares two sets of owners to determine the package IDs that have changed. The returned dictionary
        /// contains owners that were added, removed, and changed. For the "added" and "changed" cases, the owner set
        /// is the new data. For the "removed" case, the set is empty. The key of the returned dictionary is the package
        /// ID and the value is the set of owners. The returned set of owners is an array because this is the type used
        /// on the Azure Search document models.
        /// </summary>
        /// <param name="oldData">The old owner information, typically from storage.</param>
        /// <param name="newData">The new owner information, typically from gallery DB.</param>
        SortedDictionary<string, string[]> CompareOwners(
            SortedDictionary<string, SortedSet<string>> oldData,
            SortedDictionary<string, SortedSet<string>> newData);

        /// <summary>
        /// Compares two sets of popularity transfers to determine changes. The two inputs are maps of package IDs that transfer
        /// popularity away to package IDs that receive the popularity. The returned dictionary is subset of these inputs that
        /// were added, removed, or changed. For the "added" and "changed" cases, the popularity transfer set is the new data.
        /// For the "removed" case, the set is empty.
        /// </summary>
        /// <param name="oldData">The old popularity transfers, typically from storage.</param>
        /// <param name="newData">The new popularity transfers, typically from gallery DB.</param>
        SortedDictionary<string, string[]> ComparePopularityTransfers(
            SortedDictionary<string, SortedSet<string>> oldData,
            SortedDictionary<string, SortedSet<string>> newData);
    }
}
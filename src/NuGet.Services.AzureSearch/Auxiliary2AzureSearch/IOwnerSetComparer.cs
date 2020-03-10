// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    /// <summary>
    /// Used to compare two sets of owners to determine the changes.
    /// </summary>
    public interface IOwnerSetComparer
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
        SortedDictionary<string, string[]> Compare(
            SortedDictionary<string, SortedSet<string>> oldData,
            SortedDictionary<string, SortedSet<string>> newData);
    }
}
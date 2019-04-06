// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    /// <summary>
    /// Fetches the current owner information from the database.
    /// </summary>
    public interface IDatabaseOwnerFetcher
    {
        /// <summary>
        /// Fetch a mapping from package ID to set of owners for each package registration (i.e. package ID) in the
        /// gallery database.
        /// </summary>
        Task<SortedDictionary<string, SortedSet<string>>> GetPackageIdToOwnersAsync();
    }
}
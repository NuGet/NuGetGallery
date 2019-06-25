// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface IGitRepoSearcher
    {
        /// <summary>
        /// Searches for all popular C# repos, orders them in Descending order and returns a list containing their basic information
        /// </summary>
        /// <returns>List of popular C# repositories</returns>
        Task<IReadOnlyList<RepositoryInformation>> GetPopularRepositories();
    }
}

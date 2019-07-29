// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface IRepositoriesCache
    {
        /// <summary>
        /// Tries to read the cache entry of a repository.
        /// </summary>
        /// <param name="repo">Repo to read the cache entry for</param>
        /// <param name="cached">The cached entry or null if none has been created.</param>
        /// <returns>true if a cache entry has been found.</returns>
        bool TryGetCachedVersion(WritableRepositoryInformation repo, out RepositoryInformation cached);

        /// <summary>
        /// Persists the specified repository in an internal cache.
        /// </summary>
        /// <param name="repo">Repo to persist.</param>
        void Persist(RepositoryInformation repo);
    }
}

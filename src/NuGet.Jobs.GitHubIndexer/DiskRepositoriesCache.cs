// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class DiskRepositoriesCache : IRepositoriesCache
    {
        private readonly ILogger<DiskRepositoriesCache> _logger;
        public DiskRepositoriesCache(ILogger<DiskRepositoriesCache> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a cache file for the specified repo that contains the repo's dependencies.
        /// </summary>
        /// <param name="repo">Repo to persist on disk</param>
        public void Persist(RepositoryInformation repo)
        {
            var repoCacheFile = GetCachePath(repo.Id);
            _logger.LogInformation("Saving cache for repo {RepoId} to file {FileName}", repo.Id, repoCacheFile);
            File.WriteAllText(repoCacheFile, JsonConvert.SerializeObject(repo.Dependencies));
        }

        /// <summary>
        /// Tries to read the cache file of a repository.
        /// </summary>
        /// <param name="repo">Repo to read the cache file for</param>
        /// <param name="cached">The read cached version or null if none has been created.</param>
        /// <returns>true if a cache file has been found and loaded.</returns>
        public bool TryGetCachedVersion(WritableRepositoryInformation repo, out RepositoryInformation cached)
        {
            var repoCacheFile = GetCachePath(repo.Id);
            _logger.LogInformation("Cache lookup for repo {RepoId}", repo.Id);
            if (!File.Exists(repoCacheFile))
            {
                cached = null;
                return false;
            }

            _logger.LogInformation("Cache hit for repo {RepoId} on file {FileName}", repo.Id, repoCacheFile);
            repo.AddDependencies(JsonConvert.DeserializeObject<IReadOnlyList<string>>(File.ReadAllText(repoCacheFile)));
            cached = repo.ToRepositoryInformation();

            return true;
        }

        private string GetCachePath(string repoId)
        {
            return Path.Combine(ReposIndexer.CacheDirectory, $"{repoId.Replace('/', '_')}.json"); // Replacing the '/' by '_' to avoid having sub-directories
        }
    }
}

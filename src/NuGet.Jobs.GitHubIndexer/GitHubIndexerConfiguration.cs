// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubIndexerConfiguration
    {
        /// <summary>
        /// Minimum number of stars that a GitHub Repo needs to have to be included in the indexing
        /// </summary>
        public int MinStars { get; set; } = 100;

        /// <summary>
        /// The number of results that would be shown per page. This is currently limited to 100 (limit verified on 6/24/2019)
        /// </summary>
        public int ResultsPerPage { get; set; } = 100;

        /// <summary>
        /// The limit of results that a single search query can show. This is currently limited to 1000 (limit verified on 6/24/2019)
        /// </summary>
        public int MaxGitHubResultsPerQuery { get; set; } = 1000;

        /// <summary>
        /// The number of concurrent threads running to index Git repositories
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 32;
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

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

        /// <summary>
        /// If a repo takes longer to index than this timeout, we will crash the process and attempt again later.
        /// </summary>
        public TimeSpan RepoIndexingTimeout { get; set; } = TimeSpan.FromMinutes(150);

        /// <summary>
        /// The connection string to be used for a <see cref="NuGetGallery.CloudBlobClientWrapper"/> instance.
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// Gets a setting if Read Access Geo Redundant is enabled in azure storage
        /// </summary>
        public bool StorageReadAccessGeoRedundant { get; set; }

        /// <summary>
        /// How long to sleep after succeeding. If the job fails this setting will be ignored.
        /// </summary>
        public TimeSpan SleepAfterSuccess { get; set; }

        /// <summary>
        /// The list of repositories to be ignored by the indexing job. ("{repoOwner}/{repoName}")
        /// </summary>
        public HashSet<string> IgnoreList { get; set; }

        /// <summary>
        /// The maximum time allowed for a GitHub HTTP request, per <see cref="Octokit.GitHubClient.SetRequestTimeout(TimeSpan)"/>.
        /// </summary>
        public TimeSpan GitHubRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}

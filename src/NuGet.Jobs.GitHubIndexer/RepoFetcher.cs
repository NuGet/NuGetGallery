// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.GitHubIndexer
{
    public class RepoFetcher : IRepoFetcher
    {
        private readonly RepoUtils _repoUtils;
        private readonly ILoggerFactory _loggerFactory;

        public RepoFetcher(RepoUtils repoUtils, ILoggerFactory loggerFactory)
        {
            _repoUtils = repoUtils ?? throw new ArgumentNullException(nameof(repoUtils));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IFetchedRepo FetchRepo(WritableRepositoryInformation repo)
        {
            return FetchedRepo.GetInstance(repo, _repoUtils, _loggerFactory.CreateLogger<FetchedRepo>());
        }
    }
}

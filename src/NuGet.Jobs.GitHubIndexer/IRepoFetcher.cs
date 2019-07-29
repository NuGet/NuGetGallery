// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.GitHubIndexer
{
    public interface IRepoFetcher
    {
        /// <summary>
        /// Fetches a repo.
        /// </summary>
        /// <param name="repo">Repo to fetch</param>
        /// <returns>Fetched repository.</returns>
        IFetchedRepo FetchRepo(WritableRepositoryInformation repo);
    }
}

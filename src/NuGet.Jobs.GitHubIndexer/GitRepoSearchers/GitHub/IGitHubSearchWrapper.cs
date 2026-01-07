// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface IGitHubSearchWrapper
    {
        /// <summary>
        /// Queries the GitHub Repo Search Api and returns its reponse
        /// </summary>
        /// <param name="request">Request to be made to the GitHub Repo Search Api</param>
        /// <returns>Parsed reponse of the GitHub Repo Search Api</returns>
        Task<GitHubSearchApiResponse> GetResponse(SearchRepositoriesRequest request);

        /// <summary>
        /// Returns the number of remaining requests before the search gets throttled
        /// </summary>
        /// <returns>Returns the number of remaining requests or null if no info is available (no request has been done yet)</returns>
        int? GetRemainingRequestCount();
    }
}

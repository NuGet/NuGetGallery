// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearchWrapper : IGitHubSearchWrapper
    {
        private readonly IGitHubClient _client;

        public GitHubSearchWrapper(IGitHubClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public int? GetRemainingRequestCount()
        {
            var apiInfo = _client.GetLastApiInfo();
            return apiInfo?.RateLimit.Remaining;
        }

        public async Task<GitHubSearchApiResponse> GetResponse(SearchRepositoriesRequest request)
        {
            var apiResponse = await _client.Connection.Get<SearchRepositoryResult>(ApiUrls.SearchRepositories(), request.Parameters, null);
            if (!apiResponse.HttpResponse.Headers.TryGetValue("Date", out var ghStrDate)
                || !DateTime.TryParseExact(ghStrDate, "ddd',' dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ghTime))
            {
                throw new InvalidDataException("Date is required to compute the throttling time.");
            }

            if (!apiResponse.HttpResponse.Headers.TryGetValue("X-RateLimit-Reset", out var ghStrResetLimit)
                || !long.TryParse(ghStrResetLimit, out var ghResetTime))
            {
                throw new InvalidDataException("X-RateLimit-Reset is required to compute the throttling time.");
            }

            return new GitHubSearchApiResponse(
                apiResponse.Body.Items
                    .Select(repo => new RepositoryInformation(
                        $"{repo.Owner.Login}/{repo.Name}",
                        repo.HtmlUrl,
                        repo.StargazersCount,
                        repo.Description ?? "No description.",
                        Array.Empty<string>())).ToList(),
                ghTime.ToLocalTime(),
                DateTimeOffset.FromUnixTimeSeconds(ghResetTime).ToLocalTime());
        }
    }
}

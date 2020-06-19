// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

            // According to RFC 2616, Http headers are case-insensitive. We should treat them as such.
            var caseInsensitiveHeaders = apiResponse.HttpResponse.Headers
                .AsEnumerable()
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);

            if (!caseInsensitiveHeaders.TryGetValue("Date", out var ghStrDate)
                || !DateTimeOffset.TryParseExact(ghStrDate.Replace("GMT", "+0"), "ddd',' dd MMM yyyy HH:mm:ss z", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ghTime))
            {
                throw new InvalidDataException("Date is missing, has a wrong format or is not in GMT timezone");
            }

            if (!caseInsensitiveHeaders.TryGetValue("X-Ratelimit-Reset", out var ghStrResetLimit)
                || !long.TryParse(ghStrResetLimit, out var ghResetTime))
            {
                throw new InvalidDataException("X-Ratelimit-Reset is required to compute the throttling time.");
            }

            return new GitHubSearchApiResponse(
                apiResponse.Body.Items
                    .Select(repo => new WritableRepositoryInformation(
                        repo.FullName,
                        repo.HtmlUrl,
                        repo.StargazersCount,
                        repo.Description ?? "",
                        repo.DefaultBranch))
                    .ToList(),
                ghTime,
                DateTimeOffset.FromUnixTimeSeconds(ghResetTime));
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearchWrapper : IGitHubSearchWrapper
    {
        private readonly IGitHubClient _client;
        private readonly IOptionsSnapshot<GitHubIndexerConfiguration> _options;

        public GitHubSearchWrapper(IGitHubClient client, IOptionsSnapshot<GitHubIndexerConfiguration> options)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public int? GetRemainingRequestCount()
        {
            var apiInfo = _client.GetLastApiInfo();
            return apiInfo?.RateLimit.Remaining;
        }

        public async Task<GitHubSearchApiResponse> GetResponse(SearchRepositoriesRequest request)
        {
            // We execute this with a forcible timeout because we've seen this method hang in the past, despite having
            // the timeout that is built-in on IGitHubClient connection. The forcible timeout duration is set to twice
            // the timeout applied to IGitHubClient (and by extension HttpClient) to allow the built-in timeout to work,
            // if possible. If the built-in timeout does not work, this will essentially abandon the HTTP task
            // and throw an OperationCanceledException, with a custom message. If the built-in timeout does work, a
            // TaskCanceledException will be thrown.
            var apiResponse = await TaskExtensions.ExecuteWithTimeoutAsync(
                token => _client.Connection.Get<SearchRepositoryResult>(
                    ApiUrls.SearchRepositories(),
                    request.Parameters,
                    accepts: null,
                    cancellationToken: token),
                timeout: TimeSpan.FromTicks(_options.Value.GitHubRequestTimeout.Ticks * 2));

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

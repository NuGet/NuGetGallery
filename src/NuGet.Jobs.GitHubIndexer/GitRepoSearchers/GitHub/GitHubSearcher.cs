// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearcher : IGitRepoSearcher
    {
        private static readonly TimeSpan LimitExceededRetryTime = TimeSpan.FromSeconds(5);

        private readonly ITelemetryService _telemetry;
        private readonly ILogger<GitHubSearcher> _logger;
        private readonly IOptionsSnapshot<GitHubIndexerConfiguration> _configuration;
        private readonly IGitHubSearchWrapper _searchApiRequester;

        private DateTimeOffset _throttleResetTime;

        public GitHubSearcher(
            IGitHubSearchWrapper searchApiRequester,
            ITelemetryService telemetry,
            ILogger<GitHubSearcher> logger,
            IOptionsSnapshot<GitHubIndexerConfiguration> configuration)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _searchApiRequester = searchApiRequester ?? throw new ArgumentNullException(nameof(searchApiRequester));
        }

        private int _minStars => _configuration.Value.MinStars;
        private int _resultsPerPage => _configuration.Value.ResultsPerPage;
        private int _maxGithubResultPerQuery => _configuration.Value.MaxGitHubResultsPerQuery;

        /// <summary>
        /// Searches for all the C# repos that have more than 100 stars on GitHub, orders them in Descending order and returns them.
        /// </summary>
        /// <returns>List of C# repos on GitHub that have more than 100 stars</returns>
        public async Task<IReadOnlyList<WritableRepositoryInformation>> GetPopularRepositories()
        {
            using (_telemetry.TrackDiscoverRepositoriesDuration())
            {
                _logger.LogInformation("Starting search on GitHub...");
                var result = await GetResultsFromGitHub();
                return result
                    .GroupBy(x => x.Id) // Used to remove duplicate repos (since the GH Search API may return a result that we already had in memory)
                    .Select(g => g.First())
                    .OrderByDescending(x => x.Stars)
                    .ToList();
            }
        }

        private async Task CheckThrottle()
        {
            if (_searchApiRequester.GetRemainingRequestCount() == 0)
            {
                var sleepTime = _throttleResetTime - DateTimeOffset.UtcNow;
                _throttleResetTime = DateTimeOffset.UtcNow;
                if (sleepTime.TotalSeconds > 0)
                {
                    _logger.LogInformation("Waiting {TotalSeconds} seconds to cooldown.", sleepTime.TotalSeconds);
                    await Task.Delay(sleepTime);
                }

                _logger.LogInformation("Resuming search.");
            }
        }

        private async Task<IReadOnlyList<WritableRepositoryInformation>> SearchRepo(SearchRepositoriesRequest request)
        {
            _logger.LogInformation("Requesting page {Page} for stars {Stars}", request.Page, request.Stars);

            bool? error = null;
            GitHubSearchApiResponse response = null;
            while (!error.HasValue || error.Value)
            {
                try
                {
                    response = await _searchApiRequester.GetResponse(request);
                    error = false;
                }
                catch (RateLimitExceededException)
                {
                    _logger.LogWarning("Exceeded GitHub RateLimit! Waiting for {LimitExceededRetryTime} before retrying.", LimitExceededRetryTime);
                    await Task.Delay(LimitExceededRetryTime);
                }
            }

            if (_throttleResetTime < DateTimeOffset.UtcNow)
            {
                var timeToWait = response.ThrottleResetTime - response.Date;
                _throttleResetTime = DateTimeOffset.UtcNow + timeToWait;
            }

            return response.Result;
        }

        private async Task<IReadOnlyList<WritableRepositoryInformation>> GetResultsFromGitHub()
        {
            _logger.LogInformation("Starting GitHub search with configuration: {ConfigInfo}", GetConfigInfo());
            _throttleResetTime = DateTimeOffset.UtcNow;
            var upperStarBound = int.MaxValue;
            var resultList = new List<WritableRepositoryInformation>();
            var lastPage = Math.Ceiling(_maxGithubResultPerQuery / (double)_resultsPerPage);

            while (upperStarBound >= _minStars)
            {
                var page = 0;
                while (page < lastPage)
                {
                    await CheckThrottle();

                    var request = new SearchRepositoriesRequest
                    {
                        Stars = new Range(_minStars, upperStarBound),
                        Language = Language.CSharp,
                        SortField = RepoSearchSort.Stars,
                        Order = SortDirection.Descending,
                        PerPage = _resultsPerPage,
                        Page = page + 1
                    };

                    var response = await SearchRepo(request);

                    if (response == null || !response.Any())
                    {
                        _logger.LogWarning("Search request didn't return any item. Page: {Page} {ConfigInfo}", request.Page, GetConfigInfo());
                        return resultList;
                    }

                    var ignoreList = _configuration.Value.IgnoreList;
                    var filteredRepoList = response.Where(x =>
                    {
                        var isValidRepo = !ignoreList.Contains(x.Id, StringComparer.OrdinalIgnoreCase);
                        if (!isValidRepo)
                        {
                            _logger.LogInformation("Found {RepoName} in the ignore list", x.Id);
                        }
                        return isValidRepo;
                    });
                    resultList.AddRange(filteredRepoList);
                    page++;

                    if (page == (int)lastPage && response.First().Stars == response.Last().Stars)
                    {
                        // GitHub throttles us after a certain number of results per query.
                        // We can only construct queries based on number of stars a repository has.
                        // As a result, if too many repositories have the same number of stars, 
                        // we will lose data because we can't create another query that filters out the results that we have already seen with the same number of stars.
                        _logger.LogWarning("Last page results have the same star count! This may result in missing data. StarCount: {Stars} {ConfigInfo}",
                            response.First().Stars,
                            GetConfigInfo());

                        return resultList;
                    }
                }

                upperStarBound = resultList.Last().Stars;
            }

            return resultList;
        }

        private string GetConfigInfo()
        {
            return $"MinStars: {_minStars}\n" +
               $"ResultsPerPage: {_resultsPerPage}\n" +
               $"MaxGithubResultPerQuery: {_maxGithubResultPerQuery}\n";
        }
    }
}

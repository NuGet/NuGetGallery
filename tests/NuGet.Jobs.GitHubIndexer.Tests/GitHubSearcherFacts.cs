// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Octokit;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class GitHubSearcherFacts
    {
        private static GitHubSearcher GetMockClient(Mock<ITelemetryService> mockTelemetry, Func<SearchRepositoriesRequest, Task<IReadOnlyList<WritableRepositoryInformation>>> searchResultFunc = null, GitHubIndexerConfiguration configuration = null)
        {
            var mockSearchApiRequester = new Mock<IGitHubSearchWrapper>();
            mockSearchApiRequester
                .Setup(r => r.GetResponse(It.IsAny<SearchRepositoriesRequest>()))
                .Returns(async (SearchRepositoriesRequest request) =>
                {
                    return new GitHubSearchApiResponse(searchResultFunc == null ? new List<WritableRepositoryInformation>() : await searchResultFunc(request), DateTimeOffset.Now, DateTimeOffset.Now);
                });

            var optionsSnapshot = new Mock<IOptionsSnapshot<GitHubIndexerConfiguration>>();
            optionsSnapshot
                .Setup(x => x.Value)
                .Returns(() => configuration ?? new GitHubIndexerConfiguration());

            return new GitHubSearcher(
                mockSearchApiRequester.Object,
                mockTelemetry.Object,
                Mock.Of<ILogger<GitHubSearcher>>(),
                optionsSnapshot.Object);
        }

        private static Func<SearchRepositoriesRequest, Task<IReadOnlyList<WritableRepositoryInformation>>> GetMockGitHubSearch(
            List<WritableRepositoryInformation> items,
            GitHubIndexerConfiguration configuration)
        {
            return 
                 req =>
                 {
                      //Stars are split as "min..max"
                      var starsStr = req.Stars.ToString();
                     var min = int.Parse(starsStr.Substring(0, starsStr.IndexOf('.')));
                     var max = int.Parse(starsStr.Substring(starsStr.LastIndexOf('.') + 1));
                     int idxMax = -1, idxMin = items.Count;

                     for (int i = 0; i < items.Count; i++)
                     {
                         var repo = items[i];
                         if (repo.Stars <= max && idxMax == -1)
                         {
                             idxMax = i;
                         }

                         if (repo.Stars <= min)
                         {
                             idxMin = i;
                             break;
                         }
                     }

                     var page = req.Page - 1;
                     var startId = idxMax + req.PerPage * page > idxMin ? idxMin : idxMax + req.PerPage * page;

                     var itemsCount = Math.Min(configuration.ResultsPerPage, idxMin - startId); // To avoid overflowing
                      IReadOnlyList<WritableRepositoryInformation> subItems = itemsCount == 0 ? new List<WritableRepositoryInformation>() : items.GetRange(startId, itemsCount);

                     return Task.FromResult(subItems);
                 };
        }

        public class GetPopularRepositoriesMethod
        {
            private readonly GitHubIndexerConfiguration _configuration = new GitHubIndexerConfiguration();

            [Fact]
            public async Task GetZeroResult()
            {
                var mockTelemetry = new Mock<ITelemetryService>();
                var durationMetric = new Mock<IDisposable>();
                mockTelemetry
                    .Setup(t => t.TrackDiscoverRepositoriesDuration())
                    .Returns(durationMetric.Object);

                var res = await GetMockClient(mockTelemetry).GetPopularRepositories();
                Assert.Empty(res);

                mockTelemetry.Verify(t => t.TrackDiscoverRepositoriesDuration(), Times.Once);
                durationMetric.Verify(m => m.Dispose(), Times.Once);
            }

            [Theory]
            [InlineData(4000, 10, 200, 2)] // Tests huge number of pages
            [InlineData(4000, 10, 50, 2)] // Tests huge number of API calls
            [InlineData(30000, 10, 1000, 100)] // Tests huge number of results in real conditions
            public async Task GetMoreThanThousandResults(int totalCount, int minStars, int maxGithubResultPerQuery, int resultsPerPage)
            {
                var mockTelemetry = new Mock<ITelemetryService>();
                var durationMetric = new Mock<IDisposable>();
                mockTelemetry
                    .Setup(t => t.TrackDiscoverRepositoriesDuration())
                    .Returns(durationMetric.Object);

                _configuration.ResultsPerPage = resultsPerPage;
                _configuration.MinStars = minStars;
                _configuration.MaxGitHubResultsPerQuery = maxGithubResultPerQuery;
                _configuration.IgnoreList = new HashSet<string>();

                // Generate ordered results by starCount (the min starCount has to be >= GitHubSearcher.MIN_STARS)
                var items = new List<WritableRepositoryInformation>();

                int maxStars = (totalCount + _configuration.MinStars);
                for (int i = 0; i < totalCount; i++)
                {
                    items.Add(new WritableRepositoryInformation("owner/Hello" + i, "dummyUrl", maxStars - i, "Some random repo description.", "master"));
                }

                // Create a mock GitHub Search API that serves those results
                var mockGitHubSearch = GetMockGitHubSearch(items, _configuration);

                var res = await GetMockClient(mockTelemetry, mockGitHubSearch, _configuration).GetPopularRepositories();
                Assert.Equal(items.Count, res.Count);

                for (int resIdx = 0; resIdx < res.Count; resIdx++)
                {
                    var resItem = res[resIdx];
                    Assert.Equal(items[resIdx].Id, resItem.Id);
                    Assert.Equal(items[resIdx].MainBranch, resItem.MainBranch);
                    Assert.Equal(items[resIdx].Stars, resItem.Stars);
                    Assert.Equal(items[resIdx].Description, resItem.Description);
                    Assert.Equal(items[resIdx].Url, resItem.Url);
                }

                mockTelemetry.Verify(t => t.TrackDiscoverRepositoriesDuration(), Times.Once);
                durationMetric.Verify(m => m.Dispose(), Times.Once);
            }

            [Fact]
            public async Task IgnoreListTest()
            {
                const int totalCount = 5000;
                const int minStars = 10;
                const int maxGithubResultPerQuery = 50;
                const int resultsPerPage = 100;

                var mockTelemetry = new Mock<ITelemetryService>();
                var durationMetric = new Mock<IDisposable>();
                mockTelemetry
                    .Setup(t => t.TrackDiscoverRepositoriesDuration())
                    .Returns(durationMetric.Object);

                _configuration.ResultsPerPage = resultsPerPage;
                _configuration.MinStars = minStars;
                _configuration.MaxGitHubResultsPerQuery = maxGithubResultPerQuery;

                // Generate ordered results by starCount (the min starCount has to be >= GitHubSearcher.MIN_STARS)
                var items = new List<WritableRepositoryInformation>();
                var ignoreList = new HashSet<string>();

                int maxStars = (totalCount + _configuration.MinStars);
                for (int i = 0; i < totalCount; i++)
                {
                    var repoName = "owner/Hello" + i;
                    items.Add(new WritableRepositoryInformation(repoName, "dummyUrl", maxStars - i, "Some random repo description.", "master"));

                    // Add all the even numbered repos to the ignore list
                    if (i % 2 == 0)
                    {
                        // Mixing the naming case to test for case-insensitivity
                        ignoreList.Add(i % 4 == 0 ? repoName.ToUpper() : repoName);
                    }
                }

                _configuration.IgnoreList = ignoreList;

                // Create a mock GitHub Search API that serves those results
                var mockGitHubSearch = GetMockGitHubSearch(items, _configuration);

                var res = await GetMockClient(mockTelemetry, mockGitHubSearch, _configuration).GetPopularRepositories();
                var expectedFilteredResults = items.Where(x => !ignoreList.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();
                Assert.Equal(expectedFilteredResults.Count, res.Count);

                for (int resIdx = 0; resIdx < res.Count; resIdx++)
                {
                    var resItem = res[resIdx];
                    var expectedItem = expectedFilteredResults[resIdx];
                    Assert.Equal(expectedItem.Id, resItem.Id);
                    Assert.Equal(expectedItem.MainBranch, resItem.MainBranch);
                    Assert.Equal(expectedItem.Stars, resItem.Stars);
                    Assert.Equal(expectedItem.Description, resItem.Description);
                    Assert.Equal(expectedItem.Url, resItem.Url);
                }

                mockTelemetry.Verify(t => t.TrackDiscoverRepositoriesDuration(), Times.Once);
                durationMetric.Verify(m => m.Dispose(), Times.Once);
            }
        }
    }
}

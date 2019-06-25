// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGetGallery;
using Octokit;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class GitHubSearcherFacts
    {
        private static GitHubSearcher GetMockClient(Func<SearchRepositoriesRequest, Task<IReadOnlyList<RepositoryInformation>>> searchResultFunc = null, GitHubSearcherConfiguration configuration = null)
        {
            var mockSearchApiRequester = new Mock<IGitHubSearchWrapper>();
            mockSearchApiRequester
                .Setup(r => r.GetResponse(It.IsAny<SearchRepositoriesRequest>()))
                .Returns(async (SearchRepositoriesRequest request) =>
                {
                    return new GitHubSearchApiResponse(searchResultFunc == null ? new List<RepositoryInformation>() : await searchResultFunc(request), DateTimeOffset.Now, DateTimeOffset.Now);
                });

            var optionsSnapshot = new Mock<IOptionsSnapshot<GitHubSearcherConfiguration>>();
            optionsSnapshot
                .Setup(x => x.Value)
                .Returns(
                () => configuration ?? new GitHubSearcherConfiguration());

            return new GitHubSearcher(mockSearchApiRequester.Object, new Mock<ILogger<GitHubSearcher>>().Object, optionsSnapshot.Object);
        }

        public class GetPopularRepositoriesMethod
        {
            private readonly GitHubSearcherConfiguration _configuration = new GitHubSearcherConfiguration();

            [Fact]
            public async Task GetZeroResult()
            {
                var res = await GetMockClient().GetPopularRepositories();
                Assert.Empty(res);
            }

            [Theory]
            [InlineData(4000, 10, 200, 2)] // Tests huge number of pages
            [InlineData(4000, 10, 50, 2)] // Tests huge number of API calls
            [InlineData(30000, 10, 1000, 100)] // Tests huge number of results in real conditions
            public async Task GetMoreThanThousandResults(int totalCount, int minStars, int maxGithubResultPerQuery, int resultsPerPage)
            {
                _configuration.ResultsPerPage = resultsPerPage;
                _configuration.MinStars = minStars;
                _configuration.MaxGitHubResultsPerQuery = maxGithubResultPerQuery;

                // Generate ordered results by starCount (the min starCount has to be >= GitHubSearcher.MIN_STARS)
                var items = new List<RepositoryInformation>();

                int maxStars = (totalCount + _configuration.MinStars);
                for (int i = 0; i < totalCount; i++)
                {
                    items.Add(new RepositoryInformation("owner/Hello" + i, "dummyUrl", maxStars - i, Array.Empty<string>()));
                }

                // Create a mock GitHub Search API that serves those results
                Func<SearchRepositoriesRequest, Task<IReadOnlyList<RepositoryInformation>>> mockGitHubSearch =
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

                          var itemsCount = Math.Min(_configuration.ResultsPerPage, idxMin - startId); // To avoid overflowing
                          IReadOnlyList<RepositoryInformation> subItems = itemsCount == 0 ? new List<RepositoryInformation>() : items.GetRange(startId, itemsCount);

                          return Task.FromResult(subItems);
                      };

                var res = await GetMockClient(mockGitHubSearch, _configuration).GetPopularRepositories();
                Assert.Equal(items.Count, res.Count);

                for (int resIdx = 0; resIdx < res.Count; resIdx++)
                {
                    var resItem = res[resIdx];
                    Assert.Equal(items[resIdx].Name, resItem.Name);
                    Assert.Equal(items[resIdx].Id, resItem.Id);
                    Assert.Equal(items[resIdx].Stars, resItem.Stars);
                    Assert.Equal(items[resIdx].Owner, resItem.Owner);
                }
            }
        }
    }
}

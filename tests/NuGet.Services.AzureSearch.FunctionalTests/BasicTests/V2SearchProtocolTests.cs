// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core;
using BasicSearchTests.FunctionalTests.Core.Models;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class V2SearchProtocolTests : NuGetSearchFunctionalTestBase
    {
        public V2SearchProtocolTests(CommonFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture, testOutputHelper)
        {
        }

        [Fact]
        public async Task CanGetEmptyResult()
        {
            // Act
            var result = await V2SearchAsync(new V2SearchBuilder { Query = Constants.NonExistentSearchString });

            // Assert
            Assert.Equal(0, result.TotalHits);
            Assert.Empty(result.Data);
        }

        [Fact]
        public async Task ShouldGetResultsForEmptyString()
        {
            // Act
            var result = await V2SearchAsync(new V2SearchBuilder { Query = "" });

            // Assert
            Assert.True(result.TotalHits.HasValue && result.TotalHits.Value > 0, "No results found, should find at least some results for empty string query.");
            Assert.NotNull(result.Data);
        }

        [Theory]
        [MemberData(nameof(TakeResults))]
        public async Task TakeReturnsExactResults(int take)
        {
            var results = await V2SearchAsync(new V2SearchBuilder { Query = "json", Take = take });

            Assert.NotNull(results);
            Assert.True(results.TotalHits > take);
            Assert.True(results.Data.Count == take, $"The search result did not return the expected {take} results");
        }

        [Fact]
        public async Task SkipDoesSkipThePackagesInResult()
        {
            var searchTerm = "json";
            var skip = 5;
            var resultsWithoutSkip = await V2SearchAsync(new V2SearchBuilder { Query = searchTerm, Take = 10 });
            var resultsWithSkip = await V2SearchAsync(new V2SearchBuilder { Query = searchTerm, Skip = skip, Take = 10 - skip });

            Assert.NotNull(resultsWithoutSkip);
            Assert.NotNull(resultsWithSkip);
            Assert.True(resultsWithoutSkip.Data.Count > 1);
            Assert.True(resultsWithSkip.Data.Count > 1);
            Assert.True(resultsWithoutSkip.Data.Count == resultsWithSkip.Data.Count + skip);
            var packageIDListThatShouldBeSkipped = resultsWithoutSkip.Data
                .Select(x => x.PackageRegistration.Id)
                .Take(skip);
            var commonResults = resultsWithSkip.Data
                .Select(x => x.PackageRegistration.Id)
                .Where(x => packageIDListThatShouldBeSkipped.Contains(x, StringComparer.OrdinalIgnoreCase));
            Assert.True(commonResults.Count() == 0, $"Found results that should have been skipped");
        }

        [Fact]
        public async Task CountOnlyReturnsCountOfSearchResults()
        {
            var results = await V2SearchAsync(new V2SearchBuilder { CountOnly = true });

            Assert.NotNull(results);
            Assert.True(results.TotalHits > 1);
            Assert.Null(results.Data);
        }

        /// <summary>
        /// This is the query pattern used by gallery to handle "FindPackagesById()?id={id}" OData queries.
        /// </summary>
        [Fact]
        public async Task ODataFindPackagesById()
        {
            var results = await V2SearchAsync(new V2SearchBuilder
            {
                Query = $"Id:\"{Constants.TestPackageId}\"",
                Skip = 0,
                Take = 100,
                SortBy = "relevance",
                IncludeSemVer2 = true,
                Prerelease = true,
                IgnoreFilter = true,
                LuceneQuery = null,
            });

            Assert.NotNull(results);
            Assert.True(results.TotalHits >= 1);
            Assert.NotEmpty(results.Data);
            foreach (var result in results.Data)
            {
                Assert.Equal(Constants.TestPackageId, result.PackageRegistration.Id);
            }
        }

        /// <summary>
        /// This is the query pattern used by gallery to handle "Packages(Id='{id}',Version='{version}')" OData queries.
        /// </summary>
        [Fact]
        public async Task ODataSpecificPackage()
        {
            var results = await V2SearchAsync(new V2SearchBuilder
            {
                Query = $"Id:\"{Constants.TestPackageId}\" AND Version:\"{Constants.TestPackageVersion}\"",
                Skip = 0,
                Take = 1,
                SortBy = "relevance",
                IncludeSemVer2 = true,
                Prerelease = true,
                IgnoreFilter = true,
                LuceneQuery = null,
            });

            Assert.NotNull(results);
            Assert.Equal(1, results.TotalHits);
            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId, package.PackageRegistration.Id);
            Assert.Equal(Constants.TestPackageVersion, package.NormalizedVersion);
        }

        [Fact]
        public async Task ResultsHonorPreReleaseField()
        {
            var searchTerm = "json";

            var resultsWithPrerelease = await V2SearchAsync(new V2SearchBuilder { Query = searchTerm, Prerelease = true }); ;
            Assert.NotNull(resultsWithPrerelease);
            Assert.True(resultsWithPrerelease.Data.Count > 1);

            var resultsWithoutPrerelease = await V2SearchAsync(new V2SearchBuilder { Query = searchTerm, Prerelease = false });
            Assert.NotNull(resultsWithoutPrerelease);
            Assert.True(resultsWithoutPrerelease.Data.Count > 1);

            // The result count should be different for included prerelease results, 
            // else it means the search term responded with same results i.e. no results have prerelease versions
            Assert.True(resultsWithPrerelease.TotalHits > resultsWithoutPrerelease.TotalHits, 
                $"The search term {searchTerm} does not seem to have any prerelease versions in the search index.");

            var hasPrereleaseVersions = resultsWithPrerelease
                .Data
                .Any(x => IsPrerelease(x.Version));
            Assert.True(hasPrereleaseVersions, $"The search query did not return any results with the expected prerelease versions.");

            hasPrereleaseVersions = resultsWithoutPrerelease
                .Data
                .Any(x => IsPrerelease(x.Version));
            Assert.False(hasPrereleaseVersions, $"The search query returned results with prerelease versions when queried for Prerelease = false");
        }

        [Theory]
        [InlineData("packageid:" + Constants.TestPackageId_Unlisted)]
        [InlineData("packageid:" + Constants.TestPackageId_Unlisted + " version:" + Constants.TestPackageVersion_Unlisted)]
        public async Task HidesUnlistedPackagesByDefault(string query)
        {
            var searchBuilder = new V2SearchBuilder
            {
                Query = query,
            };

            var results = await V2SearchAsync(searchBuilder);

            Assert.Empty(results.Data);
        }

        [Theory]
        [InlineData("packageid:" + Constants.TestPackageId_Unlisted)]
        [InlineData("packageid:" + Constants.TestPackageId_Unlisted + " version:" + Constants.TestPackageVersion_Unlisted)]
        public async Task ShowsUnlistedPackagesWithIgnoreFilterTrue(string query)
        {
            var searchBuilder = new V2SearchBuilder
            {
                Query = query,
                IgnoreFilter = true,
            };

            var results = await V2SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId_Unlisted, package.PackageRegistration.Id);
            Assert.Equal(Constants.TestPackageVersion_Unlisted, package.Version);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ComparesVersionAsCaseInsensitive(bool ignoreFilter)
        {
            var searchBuilder = new V2SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters} version:{Constants.TestPackageVersion_SearchFilters_PrerelSemVer2.ToUpperInvariant()}",
                Prerelease = true,
                IncludeSemVer2 = true,
                IgnoreFilter = ignoreFilter,
            };

            var results = await V2SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId_SearchFilters, package.PackageRegistration.Id);
            Assert.Equal(Constants.TestPackageVersion_SearchFilters_PrerelSemVer2, package.Version);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ComparesIdAsCaseInsensitive(bool ignoreFilter)
        {
            var searchBuilder = new V2SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters.ToUpperInvariant()}",
                Prerelease = false,
                IncludeSemVer2 = false,
                IgnoreFilter = ignoreFilter,
            };

            var results = await V2SearchAsync(searchBuilder);

            var package = results.Data.OrderBy(x => x.Version).FirstOrDefault();
            Assert.NotNull(package);
            Assert.Equal(Constants.TestPackageId_SearchFilters, package.PackageRegistration.Id);
            Assert.Equal(Constants.TestPackageVersion_SearchFilters_Default, package.Version);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NormalizesVersion(bool ignoreFilter)
        {
            var searchBuilder = new V2SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters} version:1.04.0.0-delta.4+git",
                Prerelease = true,
                IncludeSemVer2 = true,
                IgnoreFilter = ignoreFilter,
            };

            var results = await V2SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId_SearchFilters, package.PackageRegistration.Id);
            Assert.Equal(Constants.TestPackageVersion_SearchFilters_PrerelSemVer2, package.Version);
        }

        [Theory]
        [InlineData(false, false, Constants.TestPackageVersion_SearchFilters_Default)]
        [InlineData(true, false, Constants.TestPackageVersion_SearchFilters_Prerel)]
        [InlineData(false, true, Constants.TestPackageVersion_SearchFilters_SemVer2)]
        [InlineData(true, true, Constants.TestPackageVersion_SearchFilters_PrerelSemVer2)]
        public async Task LatestVersionChangesWithRespectToSearchFilters(bool prerelease, bool includeSemVer2, string version)
        {
            var searchBuilder = new V2SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters}",
                Prerelease = prerelease,
                IncludeSemVer2 = includeSemVer2,
            };

            var results = await V2SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId_SearchFilters, package.PackageRegistration.Id);
            Assert.Equal(version, package.Version);
        }

        public static IEnumerable<object[]> IgnoreFilterTrueData => new[]
        {
            new
            {
                Prerelease = false,
                IncludeSemVer2 = false,
                ExpectedVersions = new[] // prerelease is always returned with ignoreFilter=true
                {
                    Constants.TestPackageVersion_SearchFilters_Default,
                    Constants.TestPackageVersion_SearchFilters_Prerel,
                },
            },
            new
            {
                Prerelease = true,
                IncludeSemVer2 = false,
                ExpectedVersions = new[]
                {
                    Constants.TestPackageVersion_SearchFilters_Default,
                    Constants.TestPackageVersion_SearchFilters_Prerel,
                },
            },
            new
            {
                Prerelease = false, 
                IncludeSemVer2 = true,
                ExpectedVersions = new[] // prerelease is always returned with ignoreFilter=true
                {
                    Constants.TestPackageVersion_SearchFilters_Default,
                    Constants.TestPackageVersion_SearchFilters_Prerel,
                    Constants.TestPackageVersion_SearchFilters_SemVer2,
                    Constants.TestPackageVersion_SearchFilters_PrerelSemVer2,
                },
            },
            new
            {
                Prerelease = true,
                IncludeSemVer2 = true,
                ExpectedVersions = new[]
                {
                    Constants.TestPackageVersion_SearchFilters_Default,
                    Constants.TestPackageVersion_SearchFilters_Prerel,
                    Constants.TestPackageVersion_SearchFilters_SemVer2,
                    Constants.TestPackageVersion_SearchFilters_PrerelSemVer2,
                },
            },
        }.Select(x => new object[] { x.Prerelease, x.IncludeSemVer2, x.ExpectedVersions });

        [Theory]
        [MemberData(nameof(IgnoreFilterTrueData))]
        public async Task IgnoreFilterTrueAlwaysIncludesPrerelease(bool prerelease, bool includeSemVer2, string[] expectedVersions)
        {
            var searchBuilder = new V2SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters}",
                Prerelease = prerelease,
                IncludeSemVer2 = includeSemVer2,
                IgnoreFilter = true,
            };

            var results = await V2SearchAsync(searchBuilder);

            Assert.Equal(
                expectedVersions,
                results.Data.Select(x => x.Version).OrderBy(x => x).ToArray());
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, false)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public async Task IgnoreFilterTrueWithSpecificIdVersionAlwaysIncludesPrerelease(bool prerelease, bool includeSemVer2, bool returned)
        {
            var searchBuilder = new V2SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters} version:{Constants.TestPackageVersion_SearchFilters_PrerelSemVer2}",
                Prerelease = prerelease,
                IncludeSemVer2 = includeSemVer2,
                IgnoreFilter = true,
            };

            var results = await V2SearchAsync(searchBuilder);

            if (returned)
            {
                var package = Assert.Single(results.Data);
                Assert.Equal(Constants.TestPackageId_SearchFilters, package.PackageRegistration.Id);
                Assert.Equal(Constants.TestPackageVersion_SearchFilters_PrerelSemVer2, package.Version);
            }
            else
            {
                Assert.Empty(results.Data);
            }
        }

        [Fact]
        public async Task SemVer2IsHiddenByDefault()
        {
            var searchTerm = "packageId:" + Constants.TestPackageId_SemVer2;
            var results = await V2SearchAsync(new V2SearchBuilder { Query = searchTerm });

            Assert.NotNull(results);
            Assert.Empty(results.Data);
        }

        [Fact]
        public async Task SemVerLevel2AllowsSemVer2Packages()
        {
            var searchTerm = "packageId:" + Constants.TestPackageId_SemVer2;
            var results = await V2SearchAsync(new V2SearchBuilder { Query = searchTerm, IncludeSemVer2 = true });

            Assert.NotNull(results);
            Assert.True(results.Data.Count >= 1);
            var atleastOneResultWithSemVer2 = results
                .Data
                .Any(x => IsSemVer2(x.Version));

            Assert.True(atleastOneResultWithSemVer2, $"The search query did not return with any semver2 results");
        }

        [Theory]
        [MemberData(nameof(GetSortByData))]
        public async Task ResultsAreOrderedBySpecifiedParameter(string orderBy, Func<V2SearchResultEntry, object> GetPropertyValue, bool reverse = false)
        {
            var results = await V2SearchAsync(new V2SearchBuilder { Query = "json", SortBy = orderBy });

            Assert.NotNull(results);
            Assert.True(results.Data.Count > 1);

            int count = results.Data.Count < 10 ? results.Data.Count : 10;
            var topResults = results
                .Data
                .Select(GetPropertyValue)
                .Take(count)
                .ToList();

            // Descending comparers
            Func<object, object, bool> comparer = (x1, x2) => {
                return topResults[0].GetType() == typeof(DateTime)
                        ? DateTime.Compare((DateTime)x1, (DateTime)x2) >= 0
                        : string.Compare(x1.ToString(), x2.ToString()) >= 0;
                };

            for (int i = 1; i < topResults.Count(); i++)
            {
                if (reverse)
                {
                    // flip for ascending comparison
                    Assert.True(comparer(topResults[i], topResults[i-1]), $"Results are not ordered in the ascending order for {orderBy} field query");
                } else
                {
                    Assert.True(comparer(topResults[i - 1], topResults[i]), $"Results are not ordered in the descending order for {orderBy} field query");
                }
            }
        }

        [Fact]
        public async Task ResultsMatchIdQueryFieldSearch()
        {
            var results = await V2SearchAsync(new V2SearchBuilder { Query = "packageId:" + Constants.TestPackageId });

            Assert.NotNull(results);
            Assert.Equal(1, results.Data.Count);
            Assert.Equal(Constants.TestPackageId, results.Data.First().PackageRegistration.Id, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ResultsMatchAuthorQueryFieldSearch()
        {
            var results = await V2SearchAsync(new V2SearchBuilder { Query = "author:" + Constants.TestPackageAuthor });

            Assert.NotNull(results);

            var authorResults = results
                .Data
                .Select(x => x.Authors);

            var resultsWithoutTestAuthor = authorResults
                .Any(x => !x.ToLower().Contains(Constants.TestPackageAuthor.ToLower()));

            Assert.False(resultsWithoutTestAuthor, $"The query returned search results without author {Constants.TestPackageAuthor}");
        }

        [Fact]
        public async Task ResultsMatchOwnersQueryFieldSearch()
        {
            var results = await V2SearchAsync(new V2SearchBuilder { Query = "owners:" + Constants.TestPackageOwner });

            Assert.NotNull(results);

            var ownerResults = results
                .Data
                .Select(x => x.PackageRegistration.Owners);

            var resultsWithoutTestOwner = ownerResults
                .Any(x => !x.Contains(Constants.TestPackageOwner, StringComparer.OrdinalIgnoreCase));

            Assert.False(resultsWithoutTestOwner, $"The query returned search results without owner {Constants.TestPackageOwner}");
        }

        [Fact]
        public async Task ResultsMatchTagsQueryFieldSearch()
        {
            var results = await V2SearchAsync(new V2SearchBuilder { Query = "tags:" + Constants.TestPackageTag });

            Assert.NotNull(results);

            var tagResults = results
                .Data
                .Select(x => x.Tags);

            var resultsWithoutTestTags = tagResults
                .Any(x => !x.ToLower().Contains(Constants.TestPackageTag.ToLower()));

            Assert.False(resultsWithoutTestTags, $"The query returned search results without tags {Constants.TestPackageTag}");
        }

        public static bool IsPrerelease(string original)
        {
            if (!NuGetVersion.TryParse(original, out var nugetVersion))
            {
                return false;
            }

            return nugetVersion.IsPrerelease;
        }

        /// <summary>
        /// Check for package version to be a SemVer2. This method only tests the version supplied.
        /// The package can still be SemVer2 if its dependency is SemVer2, however this test only tests for the 
        /// provided version.
        /// </summary>
        /// <param name="original">Version string</param>
        /// <returns>True if the provided string is SemVer2, false otherwise</returns>
        public static bool IsSemVer2(string original)
        {
            if (!NuGetVersion.TryParse(original, out var nugetVersion))
            {
                return false;
            }

            return nugetVersion.IsSemVer2;
        }

        public static IEnumerable<object[]> TakeResults
        {
            get
            {
                return Enumerable.Range(1, 10).Select(i => new object[] { i });
            }
        }

        public static IEnumerable<object[]> GetSortByData
        {
            get
            {
                yield return new object[] { "lastEdited", (Func<V2SearchResultEntry, object>)((V2SearchResultEntry data) => { return data.LastEdited; }) };
                yield return new object[] { "published", (Func<V2SearchResultEntry, object>)((V2SearchResultEntry data) => { return data.Published; }) };
                yield return new object[] { "title-asc", (Func<V2SearchResultEntry, object>)((V2SearchResultEntry data) => { return data.Title; }), true };
                yield return new object[] { "title-desc", (Func<V2SearchResultEntry, object>)((V2SearchResultEntry data) => { return data.Title; }) };
            }
        }
    }
}

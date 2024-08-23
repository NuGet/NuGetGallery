﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class V3SearchProtocolTests : NuGetSearchFunctionalTestBase
    {
        public V3SearchProtocolTests(CommonFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture, testOutputHelper)
        {
        }

        [Fact]
        public async Task CanGetEmptyResult()
        {
            // Act
            var result = await V3SearchAsync(new V3SearchBuilder { Query = Constants.NonExistentSearchString });

            // Assert
            Assert.Equal(0, result.TotalHits);
            Assert.Empty(result.Data);
        }

        [Fact]
        public async Task ShouldGetResultsForEmptyString()
        {
            // Act
            var result = await V3SearchAsync(new V3SearchBuilder { Query = "" });

            // Assert
            Assert.True(result.TotalHits.HasValue && result.TotalHits.Value > 0, "No results found, should find at least some results for empty string query.");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task EnsureTestPackageIsValid()
        {
            // Act
            var result = await V3SearchAsync(new V3SearchBuilder() { Query = Constants.TestPackageId });

            // Assert
            Assert.True(result.TotalHits.HasValue);
            Assert.True(result.TotalHits.Value > 0, $"Could not find test package {Constants.TestPackageId}");
            Assert.True(result.Data.Count > 0, $"Could not find test package {Constants.TestPackageId}");

            Assert.NotNull(result.AtContext);
            Assert.True(result.AtContext.AtVocab == "http://schema.nuget.org/schema#");
            Assert.False(string.IsNullOrEmpty(result.AtContext.AtBase));

            // Assert that the package result whose Id is "BaseTestPackage" with Version "1.0.0"
            // matches exactly what is expected.
            var package = result.Data
                .Where(p => p.Id == Constants.TestPackageId)
                .Where(p => p.Version == Constants.TestPackageVersion)
                .FirstOrDefault();

            Assert.NotNull(package);
            Assert.False(string.IsNullOrEmpty(package.AtId));
            Assert.True(package.AtType == "Package");
            Assert.False(string.IsNullOrEmpty(package.Registration));
            Assert.True(package.Description == Constants.TestPackageDescription);
            Assert.True(package.Summary == Constants.TestPackageSummary);
            Assert.True(package.Title == Constants.TestPackageTitle);
            Assert.True(package.Tags.Count() == 2);
            Assert.True(package.Tags[0] == "Tag1");
            Assert.True(package.Tags[1] == "Tag2");
            Assert.True(package.Authors.Count() == 1);
            Assert.True(package.Authors[0] == Constants.TestPackageAuthor);
            Assert.True(package.TotalDownloads != default(long));
            Assert.True(package.Versions.Count() == 1);
            Assert.True(package.Versions[0].Version == "1.0.0");
            Assert.True(package.Versions[0].Downloads != default(long));
            Assert.False(string.IsNullOrEmpty(package.Versions[0].AtId));
        }

        [Theory]
        [MemberData(nameof(TakeResults))]
        public async Task TakeReturnsExactResults(int take)
        {
            var results = await V3SearchAsync(new V3SearchBuilder { Query = "", Take = take });

            Assert.NotNull(results);
            Assert.True(results.TotalHits >= results.Data.Count);
            Assert.True(results.Data.Count == take, $"The search result did not return the expected {take} results");
        }

        [Fact]
        public async Task SkipDoesSkipThePackagesInResult()
        {
            var searchTerm = "";
            var skip = 5;
            var resultsWithoutSkip = await V3SearchAsync(new V3SearchBuilder { Query = searchTerm, Take = 10 });
            var resultsWithSkip = await V3SearchAsync(new V3SearchBuilder { Query = searchTerm, Skip = skip, Take = 10 - skip });

            Assert.NotNull(resultsWithoutSkip);
            Assert.NotNull(resultsWithSkip);
            Assert.True(resultsWithoutSkip.Data.Count > 1);
            Assert.True(resultsWithSkip.Data.Count > 1);
            Assert.True(resultsWithoutSkip.Data.Count == resultsWithSkip.Data.Count + skip);
            var packageIDListThatShouldBeSkipped = resultsWithoutSkip.Data
                .Select(x => x.Id)
                .Take(skip);
            var commonResults = resultsWithSkip.Data
                .Select(x => x.Id)
                .Where(x => packageIDListThatShouldBeSkipped.Contains(x, StringComparer.OrdinalIgnoreCase));
            Assert.True(commonResults.Count() == 0, $"Found results that should have been skipped");
        }

        [Fact]
        public async Task ResultsHonorPreReleaseField()
        {
            var searchTerm = "basetestpackage";

            var resultsWithPrerelease = await V3SearchAsync(new V3SearchBuilder { Query = searchTerm, Prerelease = true }); ;
            Assert.NotNull(resultsWithPrerelease);
            Assert.True(resultsWithPrerelease.Data.Count > 1);

            var resultsWithoutPrerelease = await V3SearchAsync(new V3SearchBuilder { Query = searchTerm, Prerelease = false });
            Assert.NotNull(resultsWithoutPrerelease);
            Assert.True(resultsWithoutPrerelease.Data.Count > 1);

            // The result count should be different for included prerelease results, 
            // else it means the search term responded with same results i.e. no results have prerelease versions
            Assert.True(resultsWithPrerelease.TotalHits > resultsWithoutPrerelease.TotalHits,
                $"The search term {searchTerm} does not seem to have any prerelease versions in the search index.");

            var hasPrereleaseVersions = resultsWithPrerelease
                .Data
                .SelectMany(x => x.Versions)
                .Any(x => TestUtilities.IsPrerelease(x.Version));
            Assert.True(hasPrereleaseVersions, $"The search query did not return any results with the expected prerelease versions.");

            hasPrereleaseVersions = resultsWithoutPrerelease
                .Data
                .SelectMany(x => x.Versions)
                .Any(x => TestUtilities.IsPrerelease(x.Version));
            Assert.False(hasPrereleaseVersions, $"The search query returned results with prerelease versions when queried for Prerelease = false");
        }

        [Theory]
        [InlineData("packageid:" + Constants.TestPackageId_Unlisted)]
        [InlineData("packageid:" + Constants.TestPackageId_Unlisted + " version:" + Constants.TestPackageVersion_Unlisted)]
        public async Task HidesUnlistedPackagesByDefault(string query)
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = query,
            };

            var results = await V3SearchAsync(searchBuilder);

            Assert.Empty(results.Data);
        }

        [Fact]
        public async Task ComparesVersionAsCaseInsensitive()
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters} version:{Constants.TestPackageVersion_SearchFilters_PrerelSemVer2.ToUpperInvariant()}",
                Prerelease = true,
                IncludeSemVer2 = true
            };

            var results = await V3SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId_SearchFilters, package.Id);
            Assert.Equal(Constants.TestPackageVersion_SearchFilters_PrerelSemVer2, package.Version);
        }

        [Fact]
        public async Task ComparesIdAsCaseInsensitive()
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters.ToUpperInvariant()}",
                Prerelease = false,
                IncludeSemVer2 = false,
            };

            var results = await V3SearchAsync(searchBuilder);

            var package = results.Data.OrderBy(x => x.Version).FirstOrDefault();
            Assert.NotNull(package);
            Assert.Equal(Constants.TestPackageId_SearchFilters, package.Id);
            Assert.Equal(Constants.TestPackageVersion_SearchFilters_Default, package.Version);
        }

        [Fact]
        public async Task NormalizesVersion()
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters} version:1.04.0.0-delta.4+git",
                Prerelease = true,
                IncludeSemVer2 = true
            };

            var results = await V3SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId_SearchFilters, package.Id);
            Assert.Equal(Constants.TestPackageVersion_SearchFilters_PrerelSemVer2, package.Version);
        }

        [Fact]
        public async Task ByDefaultReturnsAnyPackageType()
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_PackageType}",
            };

            var results = await V3SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId_PackageType, package.Id);
            Assert.Equal(Constants.TestPackageVersion_PackageType, package.Version);
            Assert.Equal("DotnetTool", Assert.Single(package.PackageTypes).Name);
        }

        [Fact]
        public async Task ExcludesPackagesWithMismatchingPackageType()
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_PackageType}",
                PackageType = "Dependency",
            };

            var results = await V3SearchAsync(searchBuilder);

            Assert.Empty(results.Data);
        }

        [Theory]
        [InlineData("DOTnettOoL", "DotnetTool", Constants.TestPackageId_PackageType)]
        [InlineData("depenDENCY", "Dependency", Constants.TestPackageId)]
        public async Task TreatsPackageTypeAsCaseInsensitive(string packageTypeQuery, string expected, string id)
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = $"packageid:{id}",
                PackageType = packageTypeQuery,
            };

            var results = await V3SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(id, package.Id);
            Assert.Equal(expected, Assert.Single(package.PackageTypes).Name);
        }

        [Fact]
        public async Task IncludesPackagesWithMatchingPackageType()
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_PackageType}",
                PackageType = "DotNetTool",
            };

            var results = await V3SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId_PackageType, package.Id);
            Assert.Equal(Constants.TestPackageVersion_PackageType, package.Version);
            Assert.Equal("DotnetTool", Assert.Single(package.PackageTypes).Name);
        }

        [Fact]
        public async Task PackagesWithoutPackageTypesAreAssumedToBeDependency()
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId}",
                PackageType = "Dependency",
            };

            var results = await V3SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId, package.Id);
            Assert.Equal(Constants.TestPackageVersion, package.Version);
            Assert.Equal("Dependency", Assert.Single(package.PackageTypes).Name);
        }

        [Fact]
        public async Task ReturnsNothingWhenThePackageTypeDoesNotExist()
        {
            var searchBuilder = new V3SearchBuilder
            {
                PackageType = Guid.NewGuid().ToString(),
            };

            var results = await V3SearchAsync(searchBuilder);

            Assert.Empty(results.Data);
        }

        [Fact]
        public async Task ReturnsNothingWhenThePackageTypeIsInvalid()
        {
            var searchBuilder = new V3SearchBuilder
            {
                PackageType = "cannot$be:a;package|type",
            };

            var results = await V3SearchAsync(searchBuilder);

            Assert.Empty(results.Data);
        }

        [Theory]
        [InlineData(false, false, Constants.TestPackageVersion_SearchFilters_Default)]
        [InlineData(true, false, Constants.TestPackageVersion_SearchFilters_Prerel)]
        [InlineData(false, true, Constants.TestPackageVersion_SearchFilters_SemVer2)]
        [InlineData(true, true, Constants.TestPackageVersion_SearchFilters_PrerelSemVer2)]
        public async Task LatestVersionChangesWithRespectToSearchFilters(bool prerelease, bool includeSemVer2, string version)
        {
            var searchBuilder = new V3SearchBuilder
            {
                Query = $"packageid:{Constants.TestPackageId_SearchFilters}",
                Prerelease = prerelease,
                IncludeSemVer2 = includeSemVer2,
            };

            var results = await V3SearchAsync(searchBuilder);

            var package = Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId_SearchFilters, package.Id);
            Assert.Equal(version, package.Version);
        }

        [Fact]
        public async Task SemVer2IsHiddenByDefault()
        {
            var searchTerm = "packageId:" + Constants.TestPackageId_SemVer2;
            var results = await V3SearchAsync(new V3SearchBuilder { Query = searchTerm });

            Assert.NotNull(results);
            Assert.Empty(results.Data);
        }

        [Fact]
        public async Task SemVerLevel2AllowsSemVer2Packages()
        {
            var searchTerm = "packageId:" + Constants.TestPackageId_SemVer2;
            var results = await V3SearchAsync(new V3SearchBuilder { Query = searchTerm, IncludeSemVer2 = true });

            Assert.NotNull(results);
            Assert.True(results.Data.Count >= 1);
            var atleastOneResultWithSemVer2 = results
                .Data
                .Any(x => TestUtilities.IsSemVer2(x.Version));

            Assert.True(atleastOneResultWithSemVer2, $"The search query did not return with any semver2 results");
        }

        [Fact]
        public async Task ResultsMatchIdQueryFieldSearch()
        {
            var results = await V3SearchAsync(new V3SearchBuilder { Query = "packageId:" + Constants.TestPackageId });

            Assert.NotNull(results);
            Assert.Single(results.Data);
            Assert.Equal(Constants.TestPackageId, results.Data.First().Id, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ResultsMatchAuthorQueryFieldSearch()
        {
            var results = await V3SearchAsync(new V3SearchBuilder { Query = "author:" + Constants.TestPackageAuthor });

            Assert.NotNull(results);

            var authorResults = results
                .Data
                .Select(x => x.Authors);

            var resultsWithoutTestAuthor = authorResults
                .Any(x => !x.Contains(Constants.TestPackageAuthor, StringComparer.OrdinalIgnoreCase));

            Assert.False(resultsWithoutTestAuthor, $"The query returned search results without author {Constants.TestPackageAuthor}");
        }

        [Fact]
        public async Task ResultsMatchOwnersQueryFieldSearch()
        {
            var results = await V3SearchAsync(new V3SearchBuilder { Query = "packageId:" + Constants.TestPackageId + " " + "owners:" + Constants.TestPackageOwner });

            Assert.NotNull(results);
            Assert.NotEmpty(results.Data);
            Assert.Equal(Constants.TestPackageId, results.Data[0].Id);
            Assert.Equal(Constants.TestPackageOwner, Assert.Single(results.Data[0].Owners));
        }

        [Fact]
        public async Task ResultsMatchTagsQueryFieldSearch()
        {
            var results = await V3SearchAsync(new V3SearchBuilder { Query = "tags:" + Constants.TestPackageTag });

            Assert.NotNull(results);

            var tagResults = results
                .Data
                .Select(x => x.Tags);

            var resultsWithoutTestTags = tagResults
                .Any(x => !x.Contains(Constants.TestPackageTag, StringComparer.OrdinalIgnoreCase));

            Assert.False(resultsWithoutTestTags, $"The query returned search results without tags {Constants.TestPackageTag}");
        }

        public static IEnumerable<object[]> TakeResults
        {
            get
            {
                return Enumerable.Range(1, 10).Select(i => new object[] { i });
            }
        }

        [Fact]
        public async Task EnablesCors()
        {
            // Arrange
            Client.DefaultRequestHeaders.Add("Origin", "https://foo.test");

            // Act
            var response = await Client.GetAsync("/query");

            // Assert
            Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
            var allowedOrigin = Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin"));
            Assert.Equal("*", allowedOrigin);

            Assert.True(response.Headers.Contains("Access-Control-Expose-Headers"));
            var exposedHeaders = Assert.Single(response.Headers.GetValues("Access-Control-Expose-Headers"));
            Assert.Equal(
                "Content-Type,Content-Length,Last-Modified,Transfer-Encoding,ETag,Date,Vary,Server,X-Hit,X-CorrelationId",
                exposedHeaders);
        }

        [Fact]
        public async Task EnsureHSTS()
        {
            // Act
            var response = await Client.GetAsync("/query");

            // Assert
            Assert.True(response.Headers.Contains("Strict-Transport-Security"));

            var HSTSHeaders = response.Headers.GetValues("Strict-Transport-Security").ToList();
            Assert.Single(HSTSHeaders);
            Assert.Equal("max-age=31536000; includeSubDomains", HSTSHeaders[0]);
        }
    }
}

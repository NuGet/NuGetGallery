// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NuGet.Services.AzureSearch.SearchService;
using Xunit;

namespace NuGet.Services.SearchService.Controllers
{
    public class SearchControllerFacts
    {
        public class IndexAsync : BaseFacts
        {
            private readonly SearchStatusResponse _status;

            public IndexAsync()
            {
                _status = new SearchStatusResponse
                {
                    Success = true,
                    Duration = TimeSpan.FromTicks(123),
                };

                _statusService
                    .Setup(x => x.GetStatusAsync(It.IsAny<SearchStatusOptions>(), It.IsAny<Assembly>()))
                    .ReturnsAsync(() => _status);
            }

            [Fact]
            public async Task DoesNotInitializeAuxiliaryDataCache()
            {
                await _target.IndexAsync();

                _auxiliaryDataCache.Verify(x => x.EnsureInitializedAsync(), Times.Never);
            }

            [Fact]
            public async Task PassesAllOptionsAndControllersAssembly()
            {
                await _target.IndexAsync();

                _statusService.Verify(x => x.GetStatusAsync(SearchStatusOptions.All, _target.GetType().Assembly), Times.Once);
                _statusService.Verify(x => x.GetStatusAsync(It.IsAny<SearchStatusOptions>(), It.IsAny<Assembly>()), Times.Once);
            }

            [Theory]
            [InlineData(false, HttpStatusCode.InternalServerError)]
            [InlineData(true, HttpStatusCode.OK)]
            public async Task ReturnsProperStatusCode(bool success, HttpStatusCode expected)
            {
                _status.Success = success;

                var response = await _target.IndexAsync();

                var result = Assert.IsType<JsonResult>(response.Result);
                Assert.Equal(expected, (HttpStatusCode)result.StatusCode);
                Assert.NotSame(_status, result.Value);
                var status = Assert.IsType<SearchStatusResponse>(result.Value);
                Assert.Null(status.Duration);
            }
        }

        public class GetStatusAsync : BaseFacts
        {
            private readonly SearchStatusResponse _status;

            public GetStatusAsync()
            {
                _status = new SearchStatusResponse { Success = true };

                _statusService
                    .Setup(x => x.GetStatusAsync(It.IsAny<SearchStatusOptions>(), It.IsAny<Assembly>()))
                    .ReturnsAsync(() => _status);
            }

            [Fact]
            public async Task DoesNotInitializeAuxiliaryDataCache()
            {
                await _target.GetStatusAsync();

                _auxiliaryDataCache.Verify(x => x.EnsureInitializedAsync(), Times.Never);
            }

            [Fact]
            public async Task PassesAllOptionsAndControllersAssembly()
            {
                await _target.GetStatusAsync();

                _statusService.Verify(x => x.GetStatusAsync(SearchStatusOptions.All, _target.GetType().Assembly), Times.Once);
                _statusService.Verify(x => x.GetStatusAsync(It.IsAny<SearchStatusOptions>(), It.IsAny<Assembly>()), Times.Once);
            }

            [Theory]
            [InlineData(false, HttpStatusCode.InternalServerError)]
            [InlineData(true, HttpStatusCode.OK)]
            public async Task ReturnsProperStatusCode(bool success, HttpStatusCode expected)
            {
                _status.Success = success;

                var response = await _target.GetStatusAsync();

                var result = Assert.IsType<JsonResult>(response.Result);
                Assert.Equal(expected, (HttpStatusCode)result.StatusCode);
                Assert.Same(_status, result.Value);
            }
        }

        public class V2SearchAsync : BaseFacts
        {
            [Fact]
            public async Task InitializesAuxiliaryDataCache()
            {
                await _target.V2SearchAsync();

                _auxiliaryDataCache.Verify(x => x.EnsureInitializedAsync(), Times.Once);
            }

            [Fact]
            public async Task HasDefaultParameters()
            {
                V2SearchRequest lastRequest = null;
                _searchService
                    .Setup(x => x.V2SearchAsync(It.IsAny<V2SearchRequest>()))
                    .ReturnsAsync(() => _v2SearchResponse)
                    .Callback<V2SearchRequest>(x => lastRequest = x);

                await _target.V2SearchAsync();

                _searchService.Verify(x => x.V2SearchAsync(It.IsAny<V2SearchRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(0, lastRequest.Skip);
                Assert.Equal(20, lastRequest.Take);
                Assert.False(lastRequest.IgnoreFilter);
                Assert.False(lastRequest.CountOnly);
                Assert.False(lastRequest.IncludePrerelease);
                Assert.False(lastRequest.IncludeSemVer2);
                Assert.False(lastRequest.IncludeTestData);
                Assert.Null(lastRequest.Query);
                Assert.True(lastRequest.LuceneQuery);
                Assert.False(lastRequest.ShowDebug);
                Assert.Null(lastRequest.PackageType);
            }

            [Fact]
            public async Task SupportsNullParameters()
            {
                V2SearchRequest lastRequest = null;
                _searchService
                    .Setup(x => x.V2SearchAsync(It.IsAny<V2SearchRequest>()))
                    .ReturnsAsync(() => _v2SearchResponse)
                    .Callback<V2SearchRequest>(x => lastRequest = x);

                await _target.V2SearchAsync(
                    skip: null,
                    take: null,
                    ignoreFilter: null,
                    countOnly: null,
                    prerelease: null,
                    semVerLevel: null,
                    q: null,
                    sortBy: null,
                    luceneQuery: null,
                    debug: null,
                    packageType: null);

                _searchService.Verify(x => x.V2SearchAsync(It.IsAny<V2SearchRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(0, lastRequest.Skip);
                Assert.Equal(20, lastRequest.Take);
                Assert.False(lastRequest.IgnoreFilter);
                Assert.False(lastRequest.CountOnly);
                Assert.False(lastRequest.IncludePrerelease);
                Assert.False(lastRequest.IncludeSemVer2);
                Assert.Null(lastRequest.Query);
                Assert.True(lastRequest.LuceneQuery);
                Assert.False(lastRequest.ShowDebug);
                Assert.Null(lastRequest.PackageType);
            }

            [Fact]
            public async Task UsesProvidedParameters()
            {
                V2SearchRequest lastRequest = null;
                _searchService
                    .Setup(x => x.V2SearchAsync(It.IsAny<V2SearchRequest>()))
                    .ReturnsAsync(() => _v2SearchResponse)
                    .Callback<V2SearchRequest>(x => lastRequest = x);

                await _target.V2SearchAsync(
                    skip: -20,
                    take: 30000,
                    ignoreFilter: true,
                    countOnly: true,
                    prerelease: true,
                    semVerLevel: "2.0.0",
                    q: "windows azure storage",
                    sortBy: "lastEdited",
                    luceneQuery: true,
                    debug: true,
                    packageType: "dotnettool");

                _searchService.Verify(x => x.V2SearchAsync(It.IsAny<V2SearchRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(-20, lastRequest.Skip);
                Assert.Equal(30000, lastRequest.Take);
                Assert.True(lastRequest.IgnoreFilter);
                Assert.True(lastRequest.CountOnly);
                Assert.True(lastRequest.IncludePrerelease);
                Assert.True(lastRequest.IncludeSemVer2);
                Assert.Equal("windows azure storage", lastRequest.Query);
                Assert.True(lastRequest.LuceneQuery);
                Assert.True(lastRequest.ShowDebug);
                Assert.Equal("dotnettool", lastRequest.PackageType);
            }

            [Theory]
            [InlineData("", V2SortBy.Popularity)]
            [InlineData(null, V2SortBy.Popularity)]
            [InlineData("  ", V2SortBy.Popularity)]
            [InlineData("not-real", V2SortBy.Popularity)]
            [InlineData("popularity", V2SortBy.Popularity)]
            [InlineData("POPULARITY", V2SortBy.Popularity)]
            [InlineData(" lastEdited ", V2SortBy.Popularity)]
            [InlineData("lastEdited", V2SortBy.LastEditedDesc)]
            [InlineData("LASTEDITED", V2SortBy.LastEditedDesc)]
            [InlineData("published", V2SortBy.PublishedDesc)]
            [InlineData("puBLISHed", V2SortBy.PublishedDesc)]
            [InlineData("title-asc", V2SortBy.SortableTitleAsc)]
            [InlineData("TITLE-asc", V2SortBy.SortableTitleAsc)]
            [InlineData("title-desc", V2SortBy.SortableTitleDesc)]
            [InlineData("title-DESC", V2SortBy.SortableTitleDesc)]
            [InlineData("CREATED", V2SortBy.Popularity)]
            [InlineData("created-asc", V2SortBy.CreatedAsc)]
            [InlineData("Created-asc", V2SortBy.CreatedAsc)]
            [InlineData("CREATED-desc", V2SortBy.CreatedDesc)]
            [InlineData("Created-desc", V2SortBy.CreatedDesc)]
            [InlineData("totalDownloads", V2SortBy.Popularity)]
            [InlineData("totalDownloads-asc", V2SortBy.TotalDownloadsAsc)]
            [InlineData("totalDownloads-desc", V2SortBy.TotalDownloadsDesc)]
            [InlineData("TotalDownloads-desc", V2SortBy.TotalDownloadsDesc)]
            public async Task ParsesSortBy(string sortBy, V2SortBy expected)
            {
                await _target.V2SearchAsync(sortBy: sortBy);

                _searchService.Verify(
                    x => x.V2SearchAsync(It.Is<V2SearchRequest>(r => r.SortBy == expected)),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(SemVerLevels))]
            public async Task ParsesSemVerLevel(string semVerLevel, bool includeSemVer2)
            {
                await _target.V2SearchAsync(semVerLevel: semVerLevel);

                _searchService.Verify(
                    x => x.V2SearchAsync(It.Is<V2SearchRequest>(r => r.IncludeSemVer2 == includeSemVer2)),
                    Times.Once);
            }
        }

        public class V3SearchAsync : BaseFacts
        {
            [Fact]
            public async Task InitializesAuxiliaryDataCache()
            {
                await _target.V3SearchAsync();

                _auxiliaryDataCache.Verify(x => x.EnsureInitializedAsync(), Times.Once);
            }

            [Fact]
            public async Task HasDefaultParameters()
            {
                V3SearchRequest lastRequest = null;
                _searchService
                    .Setup(x => x.V3SearchAsync(It.IsAny<V3SearchRequest>()))
                    .ReturnsAsync(() => _v3SearchResponse)
                    .Callback<V3SearchRequest>(x => lastRequest = x);

                await _target.V3SearchAsync();

                _searchService.Verify(x => x.V3SearchAsync(It.IsAny<V3SearchRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(0, lastRequest.Skip);
                Assert.Equal(20, lastRequest.Take);
                Assert.False(lastRequest.IncludePrerelease);
                Assert.False(lastRequest.IncludeSemVer2);
                Assert.False(lastRequest.IncludeTestData);
                Assert.Null(lastRequest.Query);
                Assert.Null(lastRequest.PackageType);
                Assert.False(lastRequest.ShowDebug);
            }

            [Fact]
            public async Task SupportsNullParameters()
            {
                V3SearchRequest lastRequest = null;
                _searchService
                    .Setup(x => x.V3SearchAsync(It.IsAny<V3SearchRequest>()))
                    .ReturnsAsync(() => _v3SearchResponse)
                    .Callback<V3SearchRequest>(x => lastRequest = x);

                await _target.V3SearchAsync(
                    skip: null,
                    take: null,
                    prerelease: null,
                    semVerLevel: null,
                    q: null,
                    packageType: null,
                    debug: null);

                _searchService.Verify(x => x.V3SearchAsync(It.IsAny<V3SearchRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(0, lastRequest.Skip);
                Assert.Equal(20, lastRequest.Take);
                Assert.False(lastRequest.IncludePrerelease);
                Assert.False(lastRequest.IncludeSemVer2);
                Assert.Null(lastRequest.Query);
                Assert.Null(lastRequest.PackageType);
                Assert.False(lastRequest.ShowDebug);
            }

            [Fact]
            public async Task UsesProvidedParameters()
            {
                V3SearchRequest lastRequest = null;
                _searchService
                    .Setup(x => x.V3SearchAsync(It.IsAny<V3SearchRequest>()))
                    .ReturnsAsync(() => _v3SearchResponse)
                    .Callback<V3SearchRequest>(x => lastRequest = x);

                await _target.V3SearchAsync(
                    skip: -20,
                    take: 30000,
                    prerelease: true,
                    semVerLevel: "2.0.0",
                    q: "windows azure storage",
                    packageType: "DotnetTool",
                    debug: true);

                _searchService.Verify(x => x.V3SearchAsync(It.IsAny<V3SearchRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(-20, lastRequest.Skip);
                Assert.Equal(30000, lastRequest.Take);
                Assert.True(lastRequest.IncludePrerelease);
                Assert.True(lastRequest.IncludeSemVer2);
                Assert.Equal("windows azure storage", lastRequest.Query);
                Assert.Equal("DotnetTool", lastRequest.PackageType);
                Assert.True(lastRequest.ShowDebug);
            }

            [Theory]
            [MemberData(nameof(SemVerLevels))]
            public async Task ParsesSemVerLevel(string semVerLevel, bool includeSemVer2)
            {
                await _target.V3SearchAsync(semVerLevel: semVerLevel);

                _searchService.Verify(
                    x => x.V3SearchAsync(It.Is<V3SearchRequest>(r => r.IncludeSemVer2 == includeSemVer2)),
                    Times.Once);
            }
        }

        public class AutocompleteAsync : BaseFacts
        {
            [Fact]
            public async Task InitializesAuxiliaryDataCache()
            {
                await _target.AutocompleteAsync();

                _auxiliaryDataCache.Verify(x => x.EnsureInitializedAsync(), Times.Once);
            }

            [Fact]
            public async Task HasDefaultParameters()
            {
                AutocompleteRequest lastRequest = null;
                _searchService
                    .Setup(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()))
                    .ReturnsAsync(() => _autocompleteResponse)
                    .Callback<AutocompleteRequest>(x => lastRequest = x);

                await _target.AutocompleteAsync();

                _searchService.Verify(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(0, lastRequest.Skip);
                Assert.Equal(20, lastRequest.Take);
                Assert.False(lastRequest.IncludePrerelease);
                Assert.False(lastRequest.IncludeSemVer2);
                Assert.False(lastRequest.IncludeTestData);
                Assert.Null(lastRequest.Query);
                Assert.False(lastRequest.ShowDebug);
                Assert.Null(lastRequest.PackageType);
                Assert.Equal(AutocompleteRequestType.PackageIds, lastRequest.Type);
            }

            [Fact]
            public async Task SupportsNullParameters()
            {
                AutocompleteRequest lastRequest = null;
                _searchService
                    .Setup(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()))
                    .ReturnsAsync(() => _autocompleteResponse)
                    .Callback<AutocompleteRequest>(x => lastRequest = x);

                await _target.AutocompleteAsync(
                    skip: null,
                    take: null,
                    prerelease: null,
                    semVerLevel: null,
                    q: null,
                    id: null,
                    packageType: null,
                    debug: null);

                _searchService.Verify(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(0, lastRequest.Skip);
                Assert.Equal(20, lastRequest.Take);
                Assert.False(lastRequest.IncludePrerelease);
                Assert.False(lastRequest.IncludeSemVer2);
                Assert.Null(lastRequest.Query);
                Assert.False(lastRequest.ShowDebug);
                Assert.Null(lastRequest.PackageType);
                Assert.Equal(AutocompleteRequestType.PackageIds, lastRequest.Type);
            }

            [Fact]
            public async Task UsesProvidedParameters()
            {
                AutocompleteRequest lastRequest = null;
                _searchService
                    .Setup(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()))
                    .ReturnsAsync(() => _autocompleteResponse)
                    .Callback<AutocompleteRequest>(x => lastRequest = x);

                await _target.AutocompleteAsync(
                    skip: -20,
                    take: 30000,
                    prerelease: true,
                    semVerLevel: "2.0.0",
                    q: "windows azure storage",
                    packageType: "DotnetTool",
                    debug: true);

                _searchService.Verify(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(-20, lastRequest.Skip);
                Assert.Equal(30000, lastRequest.Take);
                Assert.True(lastRequest.IncludePrerelease);
                Assert.True(lastRequest.IncludeSemVer2);
                Assert.Equal("windows azure storage", lastRequest.Query);
                Assert.True(lastRequest.ShowDebug);
                Assert.Equal("DotnetTool", lastRequest.PackageType);
                Assert.Equal(AutocompleteRequestType.PackageIds, lastRequest.Type);
            }

            [Fact]
            public async Task SetsPackageVersionsRequestType()
            {
                AutocompleteRequest lastRequest = null;
                _searchService
                    .Setup(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()))
                    .ReturnsAsync(() => _autocompleteResponse)
                    .Callback<AutocompleteRequest>(x => lastRequest = x);

                await _target.AutocompleteAsync(
                    skip: -20,
                    take: 30000,
                    prerelease: true,
                    semVerLevel: "2.0.0",
                    id: "windows azure storage",
                    debug: true);

                _searchService.Verify(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(-20, lastRequest.Skip);
                Assert.Equal(30000, lastRequest.Take);
                Assert.True(lastRequest.IncludePrerelease);
                Assert.True(lastRequest.IncludeSemVer2);
                Assert.Equal("windows azure storage", lastRequest.Query);
                Assert.True(lastRequest.ShowDebug);
                Assert.Equal(AutocompleteRequestType.PackageVersions, lastRequest.Type);
            }

            [Fact]
            public async Task PrefersPackageIdsRequestType()
            {
                AutocompleteRequest lastRequest = null;
                _searchService
                    .Setup(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()))
                    .ReturnsAsync(() => _autocompleteResponse)
                    .Callback<AutocompleteRequest>(x => lastRequest = x);

                await _target.AutocompleteAsync(
                    skip: -20,
                    take: 30000,
                    prerelease: true,
                    semVerLevel: "2.0.0",
                    q: "hello world",
                    id: "windows azure storage",
                    debug: true);

                _searchService.Verify(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()), Times.Once);
                Assert.NotNull(lastRequest);
                Assert.Equal(-20, lastRequest.Skip);
                Assert.Equal(30000, lastRequest.Take);
                Assert.True(lastRequest.IncludePrerelease);
                Assert.True(lastRequest.IncludeSemVer2);
                Assert.Equal("hello world", lastRequest.Query);
                Assert.True(lastRequest.ShowDebug);
                Assert.Equal(AutocompleteRequestType.PackageIds, lastRequest.Type);
            }

            [Theory]
            [MemberData(nameof(SemVerLevels))]
            public async Task ParsesSemVerLevel(string semVerLevel, bool includeSemVer2)
            {
                await _target.AutocompleteAsync(semVerLevel: semVerLevel);

                _searchService.Verify(
                    x => x.AutocompleteAsync(It.Is<AutocompleteRequest>(r => r.IncludeSemVer2 == includeSemVer2)),
                    Times.Once);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<IAuxiliaryDataCache> _auxiliaryDataCache;
            protected readonly Mock<ISearchService> _searchService;
            protected readonly Mock<ISearchStatusService> _statusService;
            protected readonly V2SearchResponse _v2SearchResponse;
            protected readonly V3SearchResponse _v3SearchResponse;
            protected readonly AutocompleteResponse _autocompleteResponse;
            protected readonly SearchController _target;

            public static IEnumerable<object[]> SemVerLevels => new[]
            {
                new object[] { null, false },
                new object[] { string.Empty, false },
                new object[] { " ", false },
                new object[] { "something-else", false },
                new object[] { "1", false },
                new object[] { "1.0.0", false },
                new object[] { " 1.0.0 ", false },
                new object[] { "2", true },
                new object[] { "2.0.0", true },
                new object[] { "  2.0.0  ", true },
                new object[] { "3", true },
                new object[] { "3.0.0-beta", true },
            };

            public BaseFacts()
            {
                _auxiliaryDataCache = new Mock<IAuxiliaryDataCache>();
                _searchService = new Mock<ISearchService>();
                _statusService = new Mock<ISearchStatusService>();

                _v2SearchResponse = new V2SearchResponse();
                _v3SearchResponse = new V3SearchResponse();

                _searchService
                    .Setup(x => x.V2SearchAsync(It.IsAny<V2SearchRequest>()))
                    .ReturnsAsync(() => _v2SearchResponse);
                _searchService
                    .Setup(x => x.V3SearchAsync(It.IsAny<V3SearchRequest>()))
                    .ReturnsAsync(() => _v3SearchResponse);
                _searchService
                    .Setup(x => x.AutocompleteAsync(It.IsAny<AutocompleteRequest>()))
                    .ReturnsAsync(() => _autocompleteResponse);

                _target = new SearchController(
                    _auxiliaryDataCache.Object,
                    _searchService.Object,
                    _statusService.Object);
            }
        }
    }
}

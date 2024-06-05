// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using System.Web.Http.Results;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure.Search;
using NuGetGallery.OData;
using NuGetGallery.OData.QueryFilter;
using NuGetGallery.TestUtils.Infrastructure;
using NuGetGallery.WebApi;
using Xunit;

namespace NuGetGallery
{
    public class FeedServiceFacts
    {
        public class TheODataResponses
        {
            public class ForV1
            {
                [Theory]
                [InlineData("https://nuget.org/api/v1/Packages", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages/$count", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages(Id='Foo',Version='1.0.0')", "application/atom+xml; type=entry; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/FindPackagesById()?id='Foo'", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Search()?searchTerm='foo'&targetFramework='net45'&includePrerelease=true", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Search()/$count?searchTerm='foo'&targetFramework='net45'&includePrerelease=true", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages?$select=Id,Version,IsLatestVersion", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages/$count?$select=Id,Version,IsLatestVersion", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages(Id='Foo',Version='1.0.0')?$select=Id,Version,IsLatestVersion", "application/atom+xml; type=entry; charset=utf-8")]
                public async Task ReturnCorrectDefaultContentTypes(string requestUrl, string responseContentType)
                {
                    using (var server = FeedServiceHelpers.SetupODataServer())
                    {
                        var client = new HttpClient(server);
                        var response = await client.GetAsync(requestUrl);

                        Assert.Equal(responseContentType, response.Content.Headers.ContentType.ToString());
                    }
                }

                [Theory]
                [InlineData("https://nuget.org/api/v1/Packages(Id='NoFoo',Version='1.0.0')")]
                public async Task Return404NotFoundForUnexistingPackage(string requestUrl)
                {
                    using (var server = FeedServiceHelpers.SetupODataServer())
                    {
                        var client = new HttpClient(server);
                        var response = await client.GetAsync(requestUrl);

                        Assert.False(response.IsSuccessStatusCode);
                    }
                }

                [Theory]
                [InlineData("https://nuget.org/api/v1/Packages", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages/$count", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages(Id='Foo',Version='1.0.0')", "application/atom+xml; type=entry; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/FindPackagesById()?id='Foo'", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Search()?searchTerm='foo'&targetFramework='net45'&includePrerelease=true", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Search()/$count?searchTerm='foo'&targetFramework='net45'&includePrerelease=true", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages?$select=Id,Version,IsLatestVersion", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages/$count?$select=Id,Version,IsLatestVersion", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v1/Packages(Id='Foo',Version='1.0.0')?$select=Id,Version,IsLatestVersion", "application/atom+xml; type=entry; charset=utf-8")]
                public async Task ReturnDefaultContentTypesEvenForCustomAcceptHeader(string requestUrl, string responseContentType)
                {
                    using (var server = FeedServiceHelpers.SetupODataServer())
                    {
                        var client = new HttpClient(server);

                        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var response = await client.SendAsync(request);

                        Assert.Equal(responseContentType, response.Content.Headers.ContentType.ToString());
                    }
                }
            }

            public class ForV2
            {
                [Theory]
                [InlineData("https://nuget.org/api/v2/Packages", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages/$count", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages(Id='Foo',Version='1.0.0')", "application/atom+xml; type=entry; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/FindPackagesById()?id='Foo'", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Search()?searchTerm='foo'&targetFramework='net45'&includePrerelease=true", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Search()/$count?searchTerm='foo'&targetFramework='net45'&includePrerelease=true", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/GetUpdates()?packageIds='Foo|Bar'&versions='0.0.1|0.0.1'&includePrerelease=false&includeAllVersions=false&targetFrameworks=''&versionConstraints=''", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/GetUpdates()/$count?packageIds='Foo|Bar'&versions='0.0.1|0.0.1'&includePrerelease=false&includeAllVersions=false&targetFrameworks=''&versionConstraints=''", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages?$select=Id,Version,IsLatestVersion", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages/$count?$select=Id,Version,IsLatestVersion", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages(Id='Foo',Version='1.0.0')?$select=Id,Version,IsLatestVersion", "application/atom+xml; type=entry; charset=utf-8")]
                public async Task ReturnCorrectDefaultContentTypes(string requestUrl, string responseContentType)
                {
                    using (var server = FeedServiceHelpers.SetupODataServer())
                    {
                        var client = new HttpClient(server);
                        var response = await client.GetAsync(requestUrl);

                        Assert.Equal(responseContentType, response.Content.Headers.ContentType.ToString());
                    }
                }

                [Theory]
                [InlineData("https://nuget.org/api/v2/Packages(Id='NoFoo',Version='1.0.0')")]
                [InlineData("https://nuget.org/api/v2/Packages(Id='Foo',Version='1.0.0')?$filter=Id%20eq%20%27SomethingElse%27")]
                [InlineData("https://nuget.org/api/v2/Packages(Id='Foo',Version='1.0.0')?$filter=Id%20eq%20%27SomethingElse%27&$select=Id")]
                public async Task Return404NotFoundForUnexistingPackage(string requestUrl)
                {
                    using (var server = FeedServiceHelpers.SetupODataServer())
                    {
                        var client = new HttpClient(server);
                        var response = await client.GetAsync(requestUrl);

                        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                    }
                }

                [Theory]
                [InlineData("https://nuget.org/api/v2/Packages", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages/$count", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages(Id='Foo',Version='1.0.0')", "application/atom+xml; type=entry; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/FindPackagesById()?id='Foo'", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Search()?searchTerm='foo'&targetFramework='net45'&includePrerelease=true", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Search()/$count?searchTerm='foo'&targetFramework='net45'&includePrerelease=true", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/GetUpdates()?packageIds='Foo|Bar'&versions='0.0.1|0.0.1'&includePrerelease=false&includeAllVersions=false&targetFrameworks=''&versionConstraints=''", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/GetUpdates()/$count?packageIds='Foo|Bar'&versions='0.0.1|0.0.1'&includePrerelease=false&includeAllVersions=false&targetFrameworks=''&versionConstraints=''", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages?$select=Id,Version,IsLatestVersion", "application/atom+xml; type=feed; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages/$count?$select=Id,Version,IsLatestVersion", "text/plain; charset=utf-8")]
                [InlineData("https://nuget.org/api/v2/Packages(Id='Foo',Version='1.0.0')?$select=Id,Version,IsLatestVersion", "application/atom+xml; type=entry; charset=utf-8")]
                public async Task ReturnDefaultContentTypesEvenForCustomAcceptHeader(string requestUrl, string responseContentType)
                {
                    using (var server = FeedServiceHelpers.SetupODataServer())
                    {
                        var client = new HttpClient(server);

                        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var response = await client.SendAsync(request);

                        Assert.Equal(responseContentType, response.Content.Headers.ContentType.ToString());
                    }
                }
            }
        }

        public class TheGetSiteRootMethod
        {
            [Theory]
            [InlineData("http://nuget.org", "http://nuget.org/")]
            [InlineData("http://nuget.org/", "http://nuget.org/")]
            public void AddsTrailingSlashes(string siteRoot, string expected)
            {
                // Arrange
                var config = new Mock<IGalleryConfigurationService>();
                config.Setup(s => s.GetSiteRoot(false)).Returns(siteRoot);
                var feed = new TestableV1Feed(Mock.Of<IReadOnlyEntityRepository<Package>>(), config.Object, Mock.Of<ISearchService>());
                feed.Request = new HttpRequestMessage(HttpMethod.Get, siteRoot);

                // Act
                var actual = feed.GetSiteRootForTest();

                // Assert
                Assert.Equal(expected, actual);
            }

            [Fact]
            public void UsesCurrentRequestToDetermineSiteRoot()
            {
                // Arrange
                var config = new Mock<IGalleryConfigurationService>();
                config.Setup(s => s.GetSiteRoot(true)).Returns("https://nuget.org").Verifiable();
                var feed = new TestableV2Feed(Mock.Of<IReadOnlyEntityRepository<Package>>(), config.Object, Mock.Of<ISearchService>());
                feed.Request = new HttpRequestMessage(HttpMethod.Get, "https://nuget.org");

                // Act
                var actual = feed.GetSiteRootForTest();

                // Assert
                Assert.Equal("https://nuget.org/", actual);
                config.Verify();
            }
        }

        public class TheV1Feed
        {
            public class TheSearchMethod
            {
                [Fact]
                public async Task V1FeedSearchDoesNotReturnPrereleasePackages()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = true
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.1-a",
                                IsPrerelease = true,
                                Listed = true
                            },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);
                    var telemetryService = new Mock<ITelemetryService>();

                    var v1Service = new TestableV1Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v1Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = (await v1Service.Search(
                        new ODataQueryOptions<V1FeedPackage>(new ODataQueryContext(NuGetODataV1FeedConfig.GetEdmModel(), typeof(V1FeedPackage)), v1Service.Request),
                        null,
                        null))
                        .ExpectQueryResult<V1FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V1FeedPackage>>();

                    // Assert
                    Assert.Equal(1, result.Count());
                    Assert.Equal("Foo", result.First().Id);
                    Assert.Equal("1.0.0", result.First().Version);
                    Assert.Equal("https://localhost:8081/packages/Foo/1.0.0", result.First().GalleryDetailsUrl);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(true), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Fact]
                public async Task V1FeedSearchCanUseSearchService()
                {
                    // Arrange
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(Enumerable.Empty<Package>().AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(true);
                    searchService
                        .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                        .ReturnsAsync(new SearchResults(0, indexTimestampUtc: null));
                    var telemetryService = new Mock<ITelemetryService>();

                    var v1Service = new TestableV1Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v1Service.RawUrl = "https://localhost:8081/";
                    v1Service.Request = new HttpRequestMessage(HttpMethod.Get, v1Service.RawUrl);
                    v1Service.Configuration = new HttpConfiguration();
                    var options = new ODataQueryOptions<V1FeedPackage>(
                        new ODataQueryContext(NuGetODataV1FeedConfig.GetEdmModel(), typeof(V1FeedPackage)),
                        v1Service.Request);

                    // Act
                    var genericResult = await v1Service.Search(options, null, null);
                    var result = genericResult
                        .ExpectQueryResult<V1FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<PageResult<V1FeedPackage>>();

                    // Assert
                    Assert.Empty(result);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(false), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                    var response = await genericResult.ExecuteAsync(CancellationToken.None);
                    Assert.Contains(GalleryConstants.CustomQueryHeaderName, response.Headers.Select(x => x.Key));
                    Assert.Equal(
                        new[] { "false" },
                        response.Headers.GetValues(GalleryConstants.CustomQueryHeaderName).ToArray());
                }

                [Fact]
                public async Task V1FeedSearchDoesNotReturnDeletedPackages()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = true,
                                PackageStatusKey = PackageStatus.Deleted,
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.1.0",
                                IsPrerelease = false,
                                Listed = true,
                                PackageStatusKey = PackageStatus.Available,
                            },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);
                    var v1Service = new TestableV1Feed(repo.Object, configuration.Object, searchService.Object);
                    v1Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = (await v1Service.Search(
                        new ODataQueryOptions<V1FeedPackage>(new ODataQueryContext(NuGetODataV1FeedConfig.GetEdmModel(), typeof(V1FeedPackage)), v1Service.Request),
                        null,
                        null))
                        .ExpectQueryResult<V1FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V1FeedPackage>>();

                    // Assert
                    Assert.Equal(1, result.Count());
                    Assert.Equal("Foo", result.First().Id);
                    Assert.Equal("1.1.0", result.First().Version);
                    Assert.Equal("https://localhost:8081/packages/Foo/1.1.0", result.First().GalleryDetailsUrl);
                }
            }

            public class TheFindPackagesByIdMethod
            {
                [Fact]
                public async Task V1FeedFindPackagesByIdReturnsUnlistedPackagesButNotPrereleasePackages()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = false
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.1-a",
                                IsPrerelease = true,
                                Listed = true
                            },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");

                    var v1Service = new TestableV1Feed(repo.Object, configuration.Object, null);
                    v1Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = (await v1Service.FindPackagesById(
                        new ODataQueryOptions<V1FeedPackage>(new ODataQueryContext(NuGetODataV1FeedConfig.GetEdmModel(), typeof(V1FeedPackage)), v1Service.Request),
                        "Foo"))
                        .ExpectQueryResult<V1FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V1FeedPackage>>();

                    // Assert
                    Assert.Equal(1, result.Count());
                    Assert.Equal("Foo", result.First().Id);
                    Assert.Equal("1.0.0", result.First().Version);
                    Assert.Equal("https://localhost:8081/packages/Foo/1.0.0", result.First().GalleryDetailsUrl);
                }

                [Fact]
                public async Task V1FeedFindPackagesByIdDoesNotReturnDeletedPackages()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = false,
                                PackageStatusKey = PackageStatus.Deleted,
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.1",
                                IsPrerelease = false,
                                Listed = true,
                                PackageStatusKey = PackageStatus.Deleted,
                            },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");

                    var v1Service = new TestableV1Feed(repo.Object, configuration.Object, null);
                    v1Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = (await v1Service.FindPackagesById(
                        new ODataQueryOptions<V1FeedPackage>(new ODataQueryContext(NuGetODataV1FeedConfig.GetEdmModel(), typeof(V1FeedPackage)), v1Service.Request),
                        "Foo"))
                        .ExpectQueryResult<V1FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V1FeedPackage>>();

                    // Assert
                    Assert.Equal(0, result.Count());
                }
            }

            public class TheODataFilter
            {
                [Fact]
                public async Task ODataQueryFilterV2Search()
                {
                    // Arrange
                    var v1Service = GetService("https://localhost:8081/");
                    v1Service.ODataQueryVerifier.V1Search = GetQueryFilter<V1FeedPackage>(false);

                    // Act
                    var result = (await v1Service.Search(
                       new ODataQueryOptions<V1FeedPackage>(new ODataQueryContext(
                           NuGetODataV1FeedConfig.GetEdmModel(),
                           typeof(V1FeedPackage)),
                           v1Service.Request)));

                    // Assert
                    var badRequest = result as BadRequestErrorMessageResult;
                    Assert.NotNull(badRequest);
                }

                [Fact]
                public void ODataQueryFilterV1Packages()
                {
                    // Arrange
                    var service = GetService("https://localhost:8081/");
                    service.ODataQueryVerifier.V1Packages = GetQueryFilter<V1FeedPackage>(false);

                    // Act
                    var result = service.Get(
                       new ODataQueryOptions<V1FeedPackage>(new ODataQueryContext(
                           NuGetODataV1FeedConfig.GetEdmModel(),
                           typeof(V1FeedPackage)),
                           service.Request));

                    // Assert
                    var badRequest = result as BadRequestErrorMessageResult;
                    Assert.NotNull(badRequest);
                }

                private TestableV1Feed GetService(string host, string arguments = "?$skip=10")
                {
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Loose);
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns(host);
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = true });
                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);
                    var v1Service = new TestableV1Feed(repo.Object, configuration.Object, searchService.Object);
                    v1Service.Request = new HttpRequestMessage(HttpMethod.Get, $"{host}{arguments}");

                    return v1Service;
                }

                private ODataQueryFilter GetQueryFilter<T>(bool allow)
                {
                    var mockODataQueryFilter = new Mock<ODataQueryFilter>();
                    mockODataQueryFilter.Setup(qf => qf.IsAllowed(It.IsAny<ODataQueryOptions<T>>())).Returns(allow);
                    return mockODataQueryFilter.Object;
                }
            }
        }

        public class TheV2Feed
        {
            public class ThePackagesCollection
            {
                [Theory]
                [InlineData("Id eq 'Foo'", 100, 2, new[] { "Foo", "Foo" }, new[] { "1.0.0", "1.0.1-a" })]
                [InlineData("Id eq 'Bar'", 1, 1, new[] { "Bar" }, new[] { "1.0.0" })]
                [InlineData("Id eq 'Bar' and IsPrerelease eq true", 100, 2, new[] { "Bar", "Bar" }, new[] { "2.0.1-a", "2.0.1-b" })]
                [InlineData("Id eq 'Bar' or Id eq 'Foo'", 100, 6, new[] { "Foo", "Foo", "Bar", "Bar", "Bar", "Bar" }, new[] { "1.0.0", "1.0.1-a", "1.0.0", "2.0.0", "2.0.1-a", "2.0.1-b" })]
                public async Task V2FeedPackagesReturnsCollection(string filter, int top, int expectedNumberOfPackages, string[] expectedIds, string[] expectedVersions)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/api/v2/Packages?$filter=" + filter + "&$top=" + top);

                    // Act
                    var result = (await v2Service.Get(new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request)))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(expectedNumberOfPackages, result.Length);
                    for (int i = 0; i < expectedIds.Length; i++)
                    {
                        var expectedId = expectedIds[i];
                        var expectedVersion = expectedVersions[i];

                        Assert.True(result.Any(p => p.Id == expectedId && p.Version == expectedVersion), string.Format("Search results did not contain {0} {1}", expectedId, expectedVersion));
                    }
                }

                [Theory]
                [InlineData("Id eq 'Foo'")]
                [InlineData("(Id eq 'Foo')")]
                [InlineData("Id eq 'Bar' and Version eq '1.0.0'")]
                [InlineData("Id eq 'Foo' and true")]
                [InlineData("Id eq 'Foo' and false")]
                public async Task V2FeedPackagesUsesSearchHijackForIdOrIdVersionQueries(string filter)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var searchService = new Mock<ExternalSearchService>(
                        MockBehavior.Loose,
                        Mock.Of<IDiagnosticsService>(),
                        Mock.Of<ISearchClient>());
                    searchService.CallBase = true;
                    searchService
                        .Setup(x => x.RawSearch(It.IsAny<SearchFilter>()))
                        .ReturnsAsync(new SearchResults(0, indexTimestampUtc: null));

                    var telemetryService = new Mock<ITelemetryService>();

                    string rawUrl = "https://localhost:8081/api/v2/Packages?$filter=" + filter + "&$top=10&$orderby=DownloadCount desc";

                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, rawUrl);
                    v2Service.RawUrl = rawUrl;
                    v2Service.Configuration = new HttpConfiguration();
                    var options = new ODataQueryOptions<V2FeedPackage>(
                        new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)),
                        v2Service.Request);

                    // Act
                    var genericResult = await v2Service.Get(options);
                    var result = genericResult
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<PageResult<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.NotNull(result);
                    searchService.Verify();
                    telemetryService.Verify(x => x.TrackODataCustomQuery(false), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                    var response = await genericResult.ExecuteAsync(CancellationToken.None);
                    Assert.Contains(GalleryConstants.CustomQueryHeaderName, response.Headers.Select(x => x.Key));
                    Assert.Equal(
                        new[] { "false" },
                        response.Headers.GetValues(GalleryConstants.CustomQueryHeaderName).ToArray());
                }

                [Theory]
                [InlineData("Id eq 'Bar' and IsPrerelease eq true")]
                [InlineData("Id eq 'Bar' or Id eq 'Foo'")]
                [InlineData("(Id eq 'Foo' and Version eq '1.0.0') or (Id eq 'Bar' and Version eq '1.0.0')")]
                [InlineData("Id eq 'NotBar' and Version eq '1.0.0' and true")]
                [InlineData("Id eq 'NotBar' and Version eq '1.0.0' and false")]
                [InlineData("true")]
                public async Task V2FeedPackagesDoesNotUseSearchHijackForFunkyQueries(string filter)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    bool called = false;
                    var searchService = new Mock<ExternalSearchService>(
                        MockBehavior.Loose,
                        Mock.Of<IDiagnosticsService>(),
                        Mock.Of<ISearchClient>());
                    searchService
                        .Setup(x => x.RawSearch(It.IsAny<SearchFilter>()))
                        .ReturnsAsync(new SearchResults(0, indexTimestampUtc: null));
                    searchService.CallBase = true;

                    var telemetryService = new Mock<ITelemetryService>();

                    string rawUrl = "https://localhost:8081/api/v2/Packages?$filter=" + filter + "&$top=10";

                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, rawUrl);
                    v2Service.RawUrl = rawUrl;
                    v2Service.Configuration = new HttpConfiguration();
                    var options = new ODataQueryOptions<V2FeedPackage>(
                        new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)),
                        v2Service.Request);

                    // Act
                    var genericResult = await v2Service.Get(options);
                    var result = genericResult
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.NotNull(result);
                    Assert.False(called); // Hijack was performed and it should not have been.
                    searchService.Verify();
                    telemetryService.Verify(x => x.TrackODataCustomQuery(true), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                    var response = await genericResult.ExecuteAsync(CancellationToken.None);
                    Assert.Contains(GalleryConstants.CustomQueryHeaderName, response.Headers.Select(x => x.Key));
                    Assert.Equal(
                        new[] { "true" },
                        response.Headers.GetValues(GalleryConstants.CustomQueryHeaderName).ToArray());
                }

                [Theory]
                [InlineData("Id eq 'Foo'", 100, 2)]
                [InlineData("Id eq 'Bar'", 1, 1)]
                [InlineData("Id eq 'Bar' and IsPrerelease eq true", 100, 2)]
                [InlineData("Id eq 'Bar' or Id eq 'Foo'", 100, 6)]
                [InlineData("Id eq 'NotBar'", 100, 0)]
                public async Task V2FeedPackagesCountReturnsCorrectCount(string filter, int top, int expectedNumberOfPackages)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/api/v2/Packages/$count?$filter=" + filter + "&$top=" + top);

                    // Act
                    var result = (await v2Service.GetCount(new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request)))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectResult<PlainTextResult>();

                    // Assert
                    Assert.Equal(expectedNumberOfPackages.ToString(), result.Content);
                }

                [Fact]
                public async Task V2FeedPackagesCountReturnsCorrectCountForDeletedPackages()
                {
                    await V2FeedPackagesCountReturnsCorrectCount("Id eq 'Baz'", 100, 0);
                }

                [Theory]
                [InlineData("Foo", "1.0.0")]
                [InlineData("Foo", "1.0.1-a")]
                [InlineData("Bar", "1.0.0")]
                [InlineData("Bar", "2.0.0")]
                [InlineData("Bar", "2.0.1-b")]
                public async Task V2FeedPackagesByIdAndVersionReturnsPackage(string expectedId, string expectedVersion)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var telemetryService = new Mock<ITelemetryService>();

                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.Request = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"https://localhost:8081/api/v2/Packages(Id=\'{expectedId}\', Version=\'{expectedVersion}\')");

                    // Act
                    var result = (await v2Service.Get(new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request), expectedId, expectedVersion))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<V2FeedPackage>();

                    // Assert
                    Assert.Equal(expectedId, result.Id);
                    Assert.Equal(expectedVersion, result.Version);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(true), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Fact]
                public async Task V2FeedPackagesByIdAndVersionCanUseTheSearchService()
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(true);
                    var data = repo.Object.GetAll().Take(1).ToArray().AsQueryable();
                    searchService
                        .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                        .ReturnsAsync(new SearchResults(
                            hits: 1,
                            indexTimestampUtc: null,
                            data: data));

                    var telemetryService = new Mock<ITelemetryService>();

                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.RawUrl = "https://localhost:8081/api/v2/Packages(Id='Foo', Version='1.0.0')";
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, v2Service.RawUrl);
                    var options = new ODataQueryOptions<V2FeedPackage>(
                        new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)),
                        v2Service.Request);

                    // Act
                    var result = (await v2Service.Get(options, "Foo", "1.0.0"))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<V2FeedPackage>();

                    // Assert
                    telemetryService.Verify(x => x.TrackODataCustomQuery(false), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Theory]
                [InlineData("NoFoo", "1.0.0")]
                [InlineData("NoBar", "1.0.0-a")]
                [InlineData("Bar", "9.9.9")]
                public async Task V2FeedPackagesByIdAndVersionReturnsNotFoundWhenPackageNotFound(string expectedId, string expectedVersion)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/api/v2/Packages(Id='" + expectedId + "', Version='" + expectedVersion + "')");

                    // Act
                    (await v2Service.Get(new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request), expectedId, expectedVersion))
                        .ExpectResult<NotFoundResult>();
                }

                [Fact]
                public async Task V2FeedPackagesByIdAndVersionReturnsNotFoundWhenPackageIsDeleted()
                {
                    await V2FeedPackagesByIdAndVersionReturnsNotFoundWhenPackageNotFound("Baz", "1.0.0");
                }

                [Theory]
                [InlineData("Id eq 'Baz'")]
                public async Task V2FeedPackagesCollectionDoesNotContainDeletedPackages(string filter)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/api/v2/Packages?$filter=" + filter);

                    // Act
                    var result = (await v2Service.Get(new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request)))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.DoesNotContain(result, p => p.Id == "Baz");
                }
            }

            public class TheFindPackagesByIdMethod
            {
                [Fact]
                public async Task V2FeedFindPackagesByIdReturnsUnlistedAndPrereleasePackages()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = false,
                                FlattenedAuthors = string.Empty,
                                Description = string.Empty,
                                Summary = string.Empty,
                                Tags = string.Empty
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.1-a",
                                IsPrerelease = true,
                                Listed = true,
                                FlattenedAuthors = string.Empty,
                                Description = string.Empty,
                                Summary = string.Empty,
                                Tags = string.Empty
                            },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);
                    var telemetryService = new Mock<ITelemetryService>();
                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = (await v2Service.FindPackagesById(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo"))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    Assert.Equal(2, result.Count());
                    Assert.Equal("Foo", result.First().Id);
                    Assert.Equal("1.0.0", result.First().Version);

                    Assert.Equal("Foo", result.Last().Id);
                    Assert.Equal("1.0.1-a", result.Last().Version);

                    telemetryService.Verify(x => x.TrackODataCustomQuery(true), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Fact]
                public async Task V2FeedFindPackagesByIdReturnsEmptyCollectionWhenNoPackages()
                {
                    // Arrange
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(() => Enumerable.Empty<Package>().AsQueryable());

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var telemetryService = new Mock<ITelemetryService>();

                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = (await v2Service.FindPackagesById(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo"))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    Assert.Equal(0, result.Count());
                    telemetryService.Verify(x => x.TrackODataCustomQuery(true), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Fact]
                public async Task V2FeedFindPackagesByIdCanUseSearchService()
                {
                    // Arrange
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(() => Enumerable.Empty<Package>().AsQueryable());

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(true);
                    searchService
                        .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                        .ReturnsAsync(new SearchResults(0, indexTimestampUtc: null));

                    var telemetryService = new Mock<ITelemetryService>();

                    var rawUrl = "https://localhost:8081/";

                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, rawUrl);
                    v2Service.RawUrl = rawUrl;
                    var options = new ODataQueryOptions<V2FeedPackage>(
                        new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)),
                        v2Service.Request);

                    // Act
                    var result = (await v2Service.FindPackagesById(options, "Foo"))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<PageResult<V2FeedPackage>>();

                    // Assert
                    Assert.Equal(0, result.Count());
                    telemetryService.Verify(x => x.TrackODataCustomQuery(false), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Fact]
                public async Task V2FeedFindPackagesConsidersSearchRequestFailureAsNonCustomQuery()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = false,
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.1",
                                IsPrerelease = false,
                                Listed = true,
                            },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(true);
                    searchService
                        .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                        .ThrowsAsync(new InvalidOperationException("Search is down."));

                    var telemetryService = new Mock<ITelemetryService>();

                    var rawUrl = "https://localhost:8081/";

                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, rawUrl);
                    v2Service.RawUrl = rawUrl;
                    v2Service.Configuration = new HttpConfiguration();
                    var options = new ODataQueryOptions<V2FeedPackage>(
                        new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)),
                        v2Service.Request);

                    // Act
                    var genericResult = await v2Service.FindPackagesById(options, packageRegistration.Id);
                    var result = genericResult
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    Assert.Equal(2, result.Count());
                    telemetryService.Verify(x => x.TrackODataCustomQuery(null), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                    var response = await genericResult.ExecuteAsync(CancellationToken.None);
                    Assert.DoesNotContain(GalleryConstants.CustomQueryHeaderName, response.Headers.Select(x => x.Key));
                }

                [Fact]
                public async Task V2FeedFindPackagesByIdDoesNotHitBackendWhenIdIsEmpty()
                {
                    // Arrange
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Loose);

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = (await v2Service.FindPackagesById(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        ""))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    repo.Verify(r => r.GetAll(), Times.Never);
                    Assert.Equal(0, result.Count());
                }

                [Fact]
                public async Task V2FeedFindPackagesByIdDoesNotReturnDeletedPackages()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = false,
                                PackageStatusKey = PackageStatus.Deleted,
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.1",
                                IsPrerelease = false,
                                Listed = true,
                                PackageStatusKey = PackageStatus.Deleted,
                            },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = (await v2Service.FindPackagesById(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo"))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    Assert.Equal(0, result.Count());
                }
            }

            public class TheSearchMethod
            {
                [Theory]
                [InlineData("Foo", false, 1, new[] { "Foo" }, new[] { "1.0.0" })]
                [InlineData("Bar", false, 2, new[] { "Bar", "Bar" }, new[] { "1.0.0", "2.0.0" })]
                [InlineData("", false, 3, new[] { "Foo", "Bar", "Bar" }, new[] { "1.0.0", "1.0.0", "2.0.0" })]
                [InlineData("CommonTag", false, 3, new[] { "Foo", "Bar", "Bar" }, new[] { "1.0.0", "1.0.0", "2.0.0" })]
                [InlineData("", true, 5, new[] { "Foo", "Foo", "Bar", "Bar", "Bar" }, new[] { "1.0.0", "1.0.1-a", "1.0.0", "2.0.0", "2.0.1-a" })]
                public async Task V2FeedSearchFiltersPackagesBySearchTermAndPrereleaseFlag(string searchTerm, bool includePrerelease, int expectedNumberOfPackages, string[] expectedIds, string[] expectedVersions)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var telemetryService = new Mock<ITelemetryService>();

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.RawUrl = "https://localhost:8081/api/v2/Search()?searchTerm='" + searchTerm + "'&targetFramework=''&includePrerelease=false";
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, v2Service.RawUrl);

                    // Act
                    var result = (await v2Service.Search(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        searchTerm: searchTerm,
                        targetFramework: null,
                        includePrerelease: includePrerelease))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(expectedNumberOfPackages, result.Length);
                    for (int i = 0; i < expectedIds.Length; i++)
                    {
                        var expectedId = expectedIds[i];
                        var expectedVersion = expectedVersions[i];

                        Assert.True(result.Any(p => p.Id == expectedId && p.Version == expectedVersion), string.Format("Search results did not contain {0} {1}", expectedId, expectedVersion));
                    }

                    telemetryService.Verify(x => x.TrackODataCustomQuery(true), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Fact]
                public async Task V2FeedSearchUsesSearchService()
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();
                    var searchTerm = "foo";
                    var includePrerelease = true;

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var telemetryService = new Mock<ITelemetryService>();

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(true);
                    searchService
                        .Setup(s => s.Search(It.IsAny<SearchFilter>()))
                        .ReturnsAsync(new SearchResults(0, indexTimestampUtc: null));

                    var v2Service = new TestableV2Feed(
                        repo.Object,
                        configuration.Object,
                        searchService.Object,
                        telemetryService.Object);
                    v2Service.RawUrl = "https://localhost:8081/api/v2/Search()?searchTerm='" + searchTerm + "'&targetFramework=''&includePrerelease=false";
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, v2Service.RawUrl);

                    // Act
                    var result = (await v2Service.Search(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        searchTerm: searchTerm,
                        targetFramework: null,
                        includePrerelease: includePrerelease))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<PageResult<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    telemetryService.Verify(x => x.TrackODataCustomQuery(false), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Theory]
                [InlineData("Foo", false, 1)]
                [InlineData("Bar", false, 2)]
                [InlineData("", false, 3)]
                [InlineData("CommonTag", false, 3)]
                [InlineData("", true, 5)]
                public async Task V2FeedSearchCountFiltersPackagesBySearchTermAndPrereleaseFlag(string searchTerm, bool includePrerelease, int expectedNumberOfPackages)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/api/v2/Search()/$count?searchTerm='" + searchTerm + "'&targetFramework=''&includePrerelease=false");

                    // Act
                    var result = (await v2Service.SearchCount(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        searchTerm: searchTerm,
                        targetFramework: null,
                        includePrerelease: includePrerelease))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectResult<PlainTextResult>();

                    // Assert
                    Assert.Equal(expectedNumberOfPackages.ToString(), result.Content);
                }

                [Fact]
                public async Task V2FeedSearchCountDoesNotCountDeletedPackages()
                {
                    await V2FeedSearchCountFiltersPackagesBySearchTermAndPrereleaseFlag("Baz", true, 0);
                }
            }

            public class TheGetUpdatesMethod
            {
                [Theory]
                [InlineData(null, "1.0.0|0.9")]
                [InlineData("", "1.0.0|0.9")]
                [InlineData("   ", "1.0.0|0.9")]
                [InlineData("|   ", "1.0.0|0.9")]
                [InlineData("A", null)]
                [InlineData("A", "")]
                [InlineData("A", "   |")]
                [InlineData("A", "|  ")]
                [InlineData("A|B", "1.0|")]
                public void V2FeedGetUpdatesReturnsEmptyResultsIfInputIsMalformed(string id, string version)
                {
                    // Arrange
                    var repo = Mock.Of<IReadOnlyEntityRepository<Package>>();
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Default);
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var telemetryService = new Mock<ITelemetryService>();
                    var v2Service = new TestableV2Feed(repo, configuration.Object, null, telemetryService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        id,
                        version,
                        includePrerelease: false,
                        includeAllVersions: true,
                        targetFrameworks: null,
                        versionConstraints: null)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    Assert.Empty(result);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(false), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Fact]
                public void V2FeedGetUpdatesIgnoresItemsWithMalformedVersions()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.0.0|abcd",
                        includePrerelease: false,
                        includeAllVersions: false,
                        targetFrameworks: null,
                        versionConstraints: null)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    Assert.Equal(1, result.Count());
                    AssertPackage(new { Id = "Foo", Version = "1.2.0" }, result.First());
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsVersionsNewerThanListedVersion()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var telemetryService = new Mock<ITelemetryService>();
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null, telemetryService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.0.0|0.9",
                        includePrerelease: false,
                        includeAllVersions: true,
                        targetFrameworks: null,
                        versionConstraints: null)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(3, result.Length);
                    AssertPackage(new { Id = "Foo", Version = "1.1.0" }, result[0]);
                    AssertPackage(new { Id = "Foo", Version = "1.2.0" }, result[1]);
                    AssertPackage(new { Id = "Qux", Version = "2.0" }, result[2]);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(false), Times.Once);
                    telemetryService.Verify(x => x.TrackODataCustomQuery(It.IsAny<bool?>()), Times.Once);
                }

                [Theory]
                [InlineData("2.3|3.5|(1.0,2.3)")]
                [InlineData("2.3")]
                [InlineData("1.0||2.0")]
                [InlineData("||")]
                [InlineData("|1.0|")]
                public void V2FeedGetUpdatesReturnsEmptyIfVersionConstraintsContainWrongNumberOfElements(string constraintString)
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "3.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(false)).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.0.0|0.9",
                        includePrerelease: false,
                        includeAllVersions: true,
                        targetFrameworks: null,
                        versionConstraints: constraintString)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Empty(result);
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsVersionsConformingToConstraints()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "3.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.0.0|0.9",
                        includePrerelease: false,
                        includeAllVersions: true,
                        targetFrameworks: null,
                        versionConstraints: "(,1.2.0)|[2.0,2.3]")
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(2, result.Length);
                    AssertPackage(new { Id = "Foo", Version = "1.1.0" }, result[0]);
                    AssertPackage(new { Id = "Qux", Version = "2.0" }, result[1]);
                }

                [Fact]
                public void V2FeedGetUpdatesIgnoreInvalidVersionConstraints()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "3.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.0.0|0.9",
                        includePrerelease: false,
                        includeAllVersions: true,
                        targetFrameworks: null,
                        versionConstraints: "(,1.2.0)|abdfsdf")
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(3, result.Length);
                    AssertPackage(new { Id = "Foo", Version = "1.1.0" }, result[0]);
                    AssertPackage(new { Id = "Qux", Version = "2.0" }, result[1]);
                    AssertPackage(new { Id = "Qux", Version = "3.0" }, result[2]);
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsVersionsConformingToConstraintsWithMissingConstraintsForSomePackges()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "3.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.0.0|0.9",
                        includePrerelease: false,
                        includeAllVersions: false,
                        targetFrameworks: null,
                        versionConstraints: "|(1.2,2.8)")
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(2, result.Length);
                    AssertPackage(new { Id = "Foo", Version = "1.2.0" }, result[0]);
                    AssertPackage(new { Id = "Qux", Version = "2.0" }, result[1]);
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsEmptyPackagesIfNoPackageSatisfiesConstraints()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "3.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.0.0|0.9",
                        includePrerelease: false,
                        includeAllVersions: false,
                        targetFrameworks: null,
                        versionConstraints: "3.4|4.0")
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    Assert.Equal(0, result.Count());
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsCaseInsensitiveMatches()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "foo",
                        "1.0.0",
                        includePrerelease: false,
                        includeAllVersions: false,
                        targetFrameworks: null,
                        versionConstraints: null)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Single(result);
                    Assert.Equal("Foo", result[0].Id);
                    Assert.Equal("1.2.0", result[0].Version);
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsUpdateIfAnyOfTheProvidedVersionsIsOlder()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "3.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Foo|Qux",
                        "1.0.0|1.2.0|0.9",
                        includePrerelease: false,
                        includeAllVersions: false,
                        targetFrameworks: null,
                        versionConstraints: null)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(2, result.Length);
                    AssertPackage(new { Id = "Foo", Version = "1.2.0" }, result[0]);
                    AssertPackage(new { Id = "Qux", Version = "3.0" }, result[1]);
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsPrereleasePackages()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.1.0|0.9",
                        includePrerelease: true,
                        includeAllVersions: true,
                        targetFrameworks: null,
                        versionConstraints: null)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(3, result.Length);
                    AssertPackage(new { Id = "Foo", Version = "1.2.0" }, result[0]);
                    AssertPackage(new { Id = "Foo", Version = "1.2.0-alpha" }, result[1]);
                    AssertPackage(new { Id = "Qux", Version = "2.0" }, result[2]);
                }

                [Fact]
                public void V2FeedGetUpdatesDoesNotReturnDeletedPackages()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, PackageStatusKey = PackageStatus.Deleted }
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo",
                        "1.0.0",
                        includePrerelease: true,
                        includeAllVersions: true,
                        targetFrameworks: null,
                        versionConstraints: null)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Empty(result);
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsResultsIfDuplicatesInPackageList()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result =
                        v2Service.GetUpdates(
                            new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                            "Foo|Qux|Foo",
                            "0.9|1.5|1.1.2",
                            includePrerelease: false,
                            includeAllVersions: true,
                            targetFrameworks: null,
                            versionConstraints: null)
                            .ExpectQueryResult<V2FeedPackage>()
                            .GetInnerResult()
                            .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                            .ToArray();

                    // Assert
                    Assert.Equal(4, result.Length);
                    AssertPackage(new { Id = "Foo", Version = "1.0.0" }, result[0]);
                    AssertPackage(new { Id = "Foo", Version = "1.1.0" }, result[1]);
                    AssertPackage(new { Id = "Foo", Version = "1.2.0" }, result[2]);
                    AssertPackage(new { Id = "Qux", Version = "2.0" }, result[3]);
                }

                [Fact]
                public void V2FeedGetUpdatesFiltersByTargetFramework()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package
                            {
                                PackageRegistration = packageRegistrationA,
                                Version = "1.1.0",
                                IsPrerelease = false,
                                Listed = true,
                                SupportedFrameworks =
                                    new[]
                                        { new PackageFramework { TargetFramework = "SL5" }, new PackageFramework { TargetFramework = "Net40-Full" } }
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistrationA,
                                Version = "1.3.0-alpha",
                                IsPrerelease = true,
                                Listed = true,
                                SupportedFrameworks =
                                    new[]
                                        { new PackageFramework { TargetFramework = "SL5" }, new PackageFramework { TargetFramework = "Net40-Full" } }
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistrationA,
                                Version = "2.0.0",
                                IsPrerelease = false,
                                Listed = true,
                                SupportedFrameworks =
                                    new[] { new PackageFramework { TargetFramework = "SL5" }, new PackageFramework { TargetFramework = "WinRT" } }
                            },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.0|1.5",
                        includePrerelease: false,
                        includeAllVersions: true,
                        targetFrameworks: "net40",
                        versionConstraints: null)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(2, result.Length);
                    AssertPackage(new { Id = "Foo", Version = "1.1.0" }, result[0]);
                    AssertPackage(new { Id = "Qux", Version = "2.0" }, result[1]);
                }

                [Fact]
                public void V2FeedGetUpdatesFiltersIncludesHighestPrereleasePackage()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package
                            {
                                PackageRegistration = packageRegistrationA,
                                Version = "1.1.0",
                                IsPrerelease = false,
                                Listed = true,
                                SupportedFrameworks =
                                    new[]
                                        { new PackageFramework { TargetFramework = "SL5" }, new PackageFramework { TargetFramework = "Net40-Full" } }
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistrationA,
                                Version = "1.2.0",
                                IsPrerelease = false,
                                Listed = true,
                                SupportedFrameworks =
                                    new[]
                                        { new PackageFramework { TargetFramework = "SL5" }, new PackageFramework { TargetFramework = "Net40-Full" } }
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistrationA,
                                Version = "1.3.0-alpha",
                                IsPrerelease = true,
                                Listed = true,
                                SupportedFrameworks =
                                    new[]
                                        { new PackageFramework { TargetFramework = "SL5" }, new PackageFramework { TargetFramework = "Net40-Full" } }
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistrationA,
                                Version = "2.0.0",
                                IsPrerelease = false,
                                Listed = true,
                                SupportedFrameworks =
                                    new[] { new PackageFramework { TargetFramework = "SL5" }, new PackageFramework { TargetFramework = "WinRT" } }
                            },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = false });
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/");

                    // Act
                    var result = v2Service.GetUpdates(
                        new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request),
                        "Foo|Qux",
                        "1.0|1.5",
                        includePrerelease: true,
                        includeAllVersions: false,
                        targetFrameworks: "net40",
                        versionConstraints: null)
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.Equal(2, result.Length);
                    AssertPackage(new { Id = "Foo", Version = "1.3.0-alpha" }, result[0]);
                    AssertPackage(new { Id = "Qux", Version = "2.0" }, result[1]);
                }
            }

            public class TheODataFilter
            {
                [Fact]
                public void ODataQueryFilterV2FeedGetUpdates()
                {
                    // Arrange
                    var v2Service = GetService("https://localhost:8081/");
                    v2Service.ODataQueryVerifier.V2GetUpdates = GetQueryFilter<V2FeedPackage>(false);

                    // Act
                    var result = v2Service.GetUpdates(
                       new ODataQueryOptions<V2FeedPackage>(
                           new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(),
                           typeof(V2FeedPackage)),
                           v2Service.Request),
                       "Pid", "Version", false, false);

                    // Assert
                    var badRequest = result as BadRequestErrorMessageResult;
                    Assert.NotNull(badRequest);
                }

                [Fact]
                public async Task ODataQueryFilterV2Search()
                {
                    // Arrange
                    var v2Service = GetService("https://localhost:8081/");
                    v2Service.ODataQueryVerifier.V2Search = GetQueryFilter<V2FeedPackage>(false);

                    // Act
                    var result = await v2Service.Search(
                       new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(
                           NuGetODataV2FeedConfig.GetEdmModel(),
                           typeof(V2FeedPackage)),
                           v2Service.Request));

                    // Assert
                    var badRequest = result as BadRequestErrorMessageResult;
                    Assert.NotNull(badRequest);
                }

                [Fact]
                public async Task ODataQueryFilterV2Packages()
                {
                    // Arrange
                    var v2Service = GetService("https://localhost:8081/");
                    v2Service.ODataQueryVerifier.V2Packages = GetQueryFilter<V2FeedPackage>(false);

                    // Act
                    var result = await v2Service.Get(
                       new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(
                           NuGetODataV2FeedConfig.GetEdmModel(),
                           typeof(V2FeedPackage)),
                           v2Service.Request));

                    // Assert
                    var badRequest = result as BadRequestErrorMessageResult;
                    Assert.NotNull(badRequest);
                }

                private TestableV2Feed GetService(string host, string arguments = "?$skip=10")
                {
                    var repo = new Mock<IReadOnlyEntityRepository<Package>>(MockBehavior.Loose);
                    var configuration = new Mock<IGalleryConfigurationService>(MockBehavior.Default);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns(host);
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Current).Returns(new AppConfiguration() { IsODataFilterEnabled = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, $"{host}{arguments}");

                    return v2Service;
                }

                private ODataQueryFilter GetQueryFilter<T>(bool allow)
                {
                    var mockODataQueryFilter = new Mock<ODataQueryFilter>();
                    mockODataQueryFilter.Setup(qf => qf.IsAllowed(It.IsAny<ODataQueryOptions<T>>())).Returns(allow);
                    return mockODataQueryFilter.Object;
                }
            }
        }

        private static void AssertPackage(dynamic expected, V2FeedPackage package)
        {
            Assert.Equal(expected.Id, package.Id);
            Assert.Equal(expected.Version, package.Version);
        }
    }
}
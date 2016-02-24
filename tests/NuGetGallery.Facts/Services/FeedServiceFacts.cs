// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using System.Web.Http.Results;
using Moq;
using NuGet.Services.Gallery;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.OData;
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
                var config = new Mock<ConfigurationService>();
                config.Setup(s => s.GetSiteRoot(false)).Returns(siteRoot);
                var feed = new TestableV1Feed(null, config.Object, null);
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
                var config = new Mock<ConfigurationService>();
                config.Setup(s => s.GetSiteRoot(true)).Returns("https://nuget.org").Verifiable();
                var feed = new TestableV2Feed(null, config.Object, null);
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
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
                    Assert.Equal("1.0.0", result.First().Version);
                    Assert.Equal("https://localhost:8081/packages/Foo/1.0.0", result.First().GalleryDetailsUrl);
                }


                [Fact]
                public async Task V1FeedSearchDoesNotReturnDeletedPackages()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = true,
                                Deleted = true
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.1.0",
                                IsPrerelease = false,
                                Listed = true,
                                Deleted = false
                            },
                    }.AsQueryable());
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = false,
                                Deleted = true
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.1",
                                IsPrerelease = false,
                                Listed = true,
                                Deleted = true
                            },
                    }.AsQueryable());
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
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
        }

        public class TheV2Feed
        {
            public class ThePackagesCollection
            {
                [Theory]
                [InlineData("Id eq 'Foo'", 100, 2, new[] { "Foo", "Foo" }, new[] { "1.0.0", "1.0.1-a" })]
                [InlineData("Id eq 'Bar'", 1, 1, new[] { "Bar" }, new[] { "1.0.0" })]
                [InlineData("Id eq 'Bar' and IsPrerelease eq true", 100, 2, new[] { "Bar", "Bar" }, new[] { "2.0.1-a" , "2.0.1-b" })]
                [InlineData("Id eq 'Bar' or Id eq 'Foo'", 100, 6, new[] { "Foo", "Foo", "Bar", "Bar", "Bar", "Bar" }, new[] { "1.0.0", "1.0.1-a", "1.0.0", "2.0.0", "2.0.1-a", "2.0.1-b" })]
                public async Task V2FeedPackagesReturnsCollection(string filter, int top, int expectedNumberOfPackages, string[] expectedIds, string[] expectedVersions)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
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
                public async Task V2FeedPackagesUsesSearchHijackForIdOrIdVersionQueries(string filter)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ExternalSearchService>(MockBehavior.Loose);
                    searchService.CallBase = true;
                    searchService.Setup(s => s.RawSearch(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)))
                            .Verifiable("Hijack was not performed");

                    string rawUrl = "https://localhost:8081/api/v2/Packages?$filter=" + filter + "&$top=10&$orderby=DownloadCount desc";

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, rawUrl);
                    v2Service.RawUrl = rawUrl;

                    // Act
                    var result = (await v2Service.Get(new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request)))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.NotNull(result);
                    searchService.Verify();
                }

                [Theory]
                [InlineData("Id eq 'Bar' and IsPrerelease eq true")]
                [InlineData("Id eq 'Bar' or Id eq 'Foo'")]
                [InlineData("(Id eq 'Foo' and Version eq '1.0.0') or (Id eq 'Bar' and Version eq '1.0.0')")]
                [InlineData("Id eq 'NotBar' and true")]
                [InlineData("true")]
                public async Task V2FeedPackagesDoesNotUseSearchHijackForFunkyQueries(string filter)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    bool called = false;
                    var searchService = new Mock<ExternalSearchService>(MockBehavior.Loose);
                    searchService.CallBase = true;
                    searchService.Setup(s => s.RawSearch(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)))
                            .Callback(() => called = true);

                    string rawUrl = "https://localhost:8081/api/v2/Packages?$filter=" + filter + "&$top=10";

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, rawUrl);
                    v2Service.RawUrl = rawUrl;

                    // Act
                    var result = (await v2Service.Get(new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request)))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>()
                        .ToArray();

                    // Assert
                    Assert.NotNull(result);
                    Assert.False(called); // Hijack was performed and it should not have been.
                    searchService.Verify();
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

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
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

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/api/v2/Packages(Id='" + expectedId + "', Version='" + expectedVersion + "')");

                    // Act
                    var result = (await v2Service.Get(new ODataQueryOptions<V2FeedPackage>(new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)), v2Service.Request), expectedId, expectedVersion))
                        .ExpectQueryResult<V2FeedPackage>()
                        .GetInnerResult()
                        .ExpectOkNegotiatedContentResult<V2FeedPackage>();

                    // Assert
                    Assert.Equal(expectedId, result.Id);
                    Assert.Equal(expectedVersion, result.Version);
                }

                [Theory]
                [InlineData("NoFoo", "1.0.0")]
                [InlineData("NoBar", "1.0.0-a")]
                [InlineData("Bar", "9.9.9")]
                public async Task V2FeedPackagesByIdAndVersionReturnsNotFoundWhenPackageNotFound(string expectedId, string expectedVersion)
                {
                    // Arrange
                    var repo = FeedServiceHelpers.SetupTestPackageRepository();

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
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

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
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
                    Assert.False(result.Any(p => p.Id == "Baz"));
                }
            }

            public class TheFindPackagesByIdMethod
            {
                [Fact]
                public async Task V2FeedFindPackagesByIdReturnsUnlistedAndPrereleasePackages()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);
                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
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
                }

                [Fact]
                public async Task V2FeedFindPackagesByIdReturnsEmptyCollectionWhenNoPackages()
                {
                    // Arrange
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(() => Enumerable.Empty<Package>().AsQueryable());

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
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

                [Fact]
                public async Task V2FeedFindPackagesByIdDoesNotReturnDeletedPackages()
                {
                    // Arrange
                    var packageRegistration = new PackageRegistration { Id = "Foo" };
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(new[]
                    {
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.0",
                                IsPrerelease = false,
                                Listed = false,
                                Deleted = true
                            },
                        new Package
                            {
                                PackageRegistration = packageRegistration,
                                Version = "1.0.1",
                                IsPrerelease = false,
                                Listed = true,
                                Deleted = true
                            },
                    }.AsQueryable());
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
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

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
                    searchService.Setup(s => s.ContainsAllVersions).Returns(false);

                    var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);
                    v2Service.Request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:8081/api/v2/Search()?searchTerm='" + searchTerm + "'&targetFramework=''&includePrerelease=false");

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

                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

                    var searchService = new Mock<ISearchService>(MockBehavior.Strict);
                    searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                        <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
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
                    var repo = Mock.Of<IEntityRepository<Package>>();
                    var configuration = Mock.Of<ConfigurationService>();
                    var v2Service = new TestableV2Feed(repo, configuration, null);
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
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    Assert.Empty(result);
                }

                [Fact]
                public void V2FeedGetUpdatesIgnoresItemsWithMalformedVersions()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(false)).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                        .ExpectOkNegotiatedContentResult<IQueryable<V2FeedPackage>>();

                    // Assert
                    Assert.Equal(0, result.Count());
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsVersionsConformingToConstraints()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    Assert.Equal(1, result.Length);
                    Assert.Equal("Foo", result[0].Id);
                    Assert.Equal("1.2.0", result[0].Version);
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsUpdateIfAnyOfTheProvidedVersionsIsOlder()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Deleted = true }
                    }.AsQueryable());
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    Assert.Equal(0, result.Length);
                }

                [Fact]
                public void V2FeedGetUpdatesReturnsResultsIfDuplicatesInPackageList()
                {
                    // Arrange
                    var packageRegistrationA = new PackageRegistration { Id = "Foo" };
                    var packageRegistrationB = new PackageRegistration { Id = "Qux" };
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                    repo.Setup(r => r.GetAll()).Returns(
                        new[]
                    {
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.0.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.1.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0-alpha", IsPrerelease = true, Listed = true },
                        new Package { PackageRegistration = packageRegistrationA, Version = "1.2.0", IsPrerelease = false, Listed = true },
                        new Package { PackageRegistration = packageRegistrationB, Version = "2.0", IsPrerelease = false, Listed = true },
                    }.AsQueryable());
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
                    var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
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
                    var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
                    configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
                    configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });
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
        }

        private static void AssertPackage(dynamic expected, V2FeedPackage package)
        {
            Assert.Equal(expected.Id, package.Id);
            Assert.Equal(expected.Version, package.Version);
        }
    }
}
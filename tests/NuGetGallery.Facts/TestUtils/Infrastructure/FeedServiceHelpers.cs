// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Dependencies;
using System.Web.Http.Routing;
using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.Controllers;

namespace NuGetGallery.TestUtils.Infrastructure
{
    public static class FeedServiceHelpers
    {
        public static HttpContextBase GetMockContext(bool isSecure = false)
        {
            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.Setup(s => s.IsSecureConnection).Returns(isSecure);
            var httpContext = new Mock<HttpContextBase>();
            httpContext.Setup(s => s.Request).Returns(httpRequest.Object);

            return httpContext.Object;
        }

        public static Mock<IEntityRepository<Package>> SetupTestPackageRepository()
        {
            var fooPackage = new PackageRegistration { Id = "Foo" };
            var barPackage = new PackageRegistration { Id = "Bar" };
            var bazPackage = new PackageRegistration { Id = "Baz" };

            var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            repo.Setup(r => r.GetAll()).Returns(new[]
            {
                new Package
                {
                    PackageRegistration = fooPackage,
                    Version = "1.0.0",
                    IsPrerelease = false,
                    Listed = true,
                    DownloadStatistics = new List<PackageStatistics>(),
                    Authors = new [] { new PackageAuthor { Name = "Test "} },
                    FlattenedAuthors = "Test",
                    Description = "Foo",
                    Summary = "Foo",
                    Tags = "Foo CommonTag"
                },
                new Package
                {
                    PackageRegistration = fooPackage,
                    Version = "1.0.1-a",
                    IsPrerelease = true,
                    Listed = true,
                    DownloadStatistics = new List<PackageStatistics>(),
                    Authors = new [] { new PackageAuthor { Name = "Test "} },
                    FlattenedAuthors = "Test",
                    Description = "Foo",
                    Summary = "Foo",
                    Tags = "Foo CommonTag"
                },
                new Package
                {
                    PackageRegistration = barPackage,
                    Version = "1.0.0",
                    IsPrerelease = false,
                    Listed = true,
                    DownloadStatistics = new List<PackageStatistics>(),
                    Authors = new [] { new PackageAuthor { Name = "Test "} },
                    FlattenedAuthors = "Test",
                    Description = "Bar",
                    Summary = "Bar",
                    Tags = "Bar CommonTag"
                },
                new Package
                {
                    PackageRegistration = barPackage,
                    Version = "2.0.0",
                    IsPrerelease = false,
                    Listed = true,
                    DownloadStatistics = new List<PackageStatistics>(),
                    Authors = new [] { new PackageAuthor { Name = "Test "} },
                    FlattenedAuthors = "Test",
                    Description = "Bar",
                    Summary = "Bar",
                    Tags = "Bar CommonTag"
                },
                new Package
                {
                    PackageRegistration = barPackage,
                    Version = "2.0.1-a",
                    IsPrerelease = true,
                    Listed = true,
                    DownloadStatistics = new List<PackageStatistics>(),
                    Authors = new [] { new PackageAuthor { Name = "Test "} },
                    FlattenedAuthors = "Test",
                    Description = "Bar",
                    Summary = "Bar",
                    Tags = "Bar CommonTag"
                },
                new Package
                {
                    PackageRegistration = barPackage,
                    Version = "2.0.1-b",
                    IsPrerelease = true,
                    Listed = false,
                    DownloadStatistics = new List<PackageStatistics>(),
                    Authors = new [] { new PackageAuthor { Name = "Test "} },
                    FlattenedAuthors = "Test",
                    Description = "Bar",
                    Summary = "Bar",
                    Tags = "Bar CommonTag"
                },
                new Package
                {
                    PackageRegistration = bazPackage,
                    Version = "1.0.0",
                    IsPrerelease = false,
                    Listed = false,
                    Deleted = true, // plot twist: this package is a soft-deleted one
                    DownloadStatistics = new List<PackageStatistics>(),
                    Authors = new [] { new PackageAuthor { Name = "Test "} },
                    FlattenedAuthors = "Test",
                    Description = "Baz",
                    Summary = "Baz",
                    Tags = "Baz CommonTag"
                }
            }.AsQueryable());

            return repo;
        }

        public static HttpServer SetupServer(IDependencyResolver dependencyResolver)
        {
            var configuration = new HttpConfiguration();
            configuration.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;
            configuration.DependencyResolver = dependencyResolver;

            WebApiConfig.Register(configuration);
            NuGetODataConfig.Register(configuration);

            return new HttpServer(configuration);
        }

        public static HttpServer SetupODataServer(TestDependencyResolver dependencyResolver = null)
        {
            // Controllers
            var repo = SetupTestPackageRepository();

            var configuration = new Mock<ConfigurationService>(MockBehavior.Strict);
            configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://nuget.org/");
            configuration.Setup(c => c.Features).Returns(new FeatureConfiguration() { FriendlyLicenses = true });

            var searchService = new Mock<ISearchService>(MockBehavior.Strict);
            searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns
                <IQueryable<Package>, string>((_, __) => Task.FromResult(new SearchResults(_.Count(), DateTime.UtcNow, _)));
            searchService.Setup(s => s.ContainsAllVersions).Returns(false);

            var v1Service = new TestableV1Feed(repo.Object, configuration.Object, searchService.Object);
            var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);

            if (dependencyResolver == null)
            {
                dependencyResolver = new TestDependencyResolver();
            }
            dependencyResolver.RegisterService(typeof(ODataV1FeedController), v1Service);
            dependencyResolver.RegisterService(typeof(ODataV2FeedController), v2Service);

            // Create server
            var server = SetupServer(dependencyResolver);

            // Additional routes needed in tests
            server.Configuration.Routes.Add("v1" + RouteName.DownloadPackage, new HttpRoute("api/v1/package/{id}/{version}"));
            server.Configuration.Routes.Add("v2" + RouteName.DownloadPackage, new HttpRoute("api/v2/package/{id}/{version}"));

            // Ready to go
            return server;
        }

    }
}
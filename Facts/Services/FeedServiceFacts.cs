﻿using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;

namespace NuGetGallery.Services
{
    public class FeedServiceFacts
    {
        [Fact]
        public void V1FeedSearchDoesNotReturnPrereleasePackages()
        {
            // Arrange
            var packageRegistration = new PackageRegistration { Id = "Foo" };
            var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            repo.Setup(r => r.GetAll()).Returns(new[] {
                new Package { PackageRegistration = packageRegistration, Version = "1.0.0", IsPrerelease = false, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
                new Package { PackageRegistration = packageRegistration, Version = "1.0.1-a", IsPrerelease = true, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
            }.AsQueryable());
            var configuration = new Mock<IConfiguration>(MockBehavior.Strict);
            configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
            var searchService = new Mock<ISearchService>(MockBehavior.Strict);
            searchService.Setup(s => s.SearchWithRelevance(It.IsAny<IQueryable<Package>>(), It.IsAny<String>())).Returns<IQueryable<Package>, string>((_, __) => _);
            var v1Service = new TestableV1Feed(repo.Object, configuration.Object, searchService.Object);

            // Act
            var result = v1Service.Search(null, null);

            // Assert
            Assert.Equal(1, result.Count());
            Assert.Equal("Foo", result.First().Id);
            Assert.Equal("1.0.0", result.First().Version);
            Assert.Equal("https://localhost:8081/packages/Foo/1.0.0", result.First().GalleryDetailsUrl);
        }

        [Fact]
        public void V1FeedSearchDoesNotReturnUnlistedPackages()
        {
            // Arrange
            var packageRegistration = new PackageRegistration { Id = "Foo" };
            var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            repo.Setup(r => r.GetAll()).Returns(new[] {
                new Package { PackageRegistration = packageRegistration, Version = "1.0.0", IsPrerelease = false, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
                new Package { PackageRegistration = packageRegistration, Version = "1.0.1-a", IsPrerelease = true, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
                new Package { PackageRegistration = new PackageRegistration { Id ="baz" }, Version = "2.0", Listed = false, DownloadStatistics = new List<PackageStatistics>() },
            }.AsQueryable());
            var searchService = new Mock<ISearchService>(MockBehavior.Strict);
            searchService.Setup(s => s.SearchWithRelevance(It.IsAny<IQueryable<Package>>(), It.IsAny<String>())).Returns<IQueryable<Package>, string>((_, __) => _);
            var configuration = new Mock<IConfiguration>(MockBehavior.Strict);
            configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("http://test.nuget.org/");
            var v1Service = new TestableV1Feed(repo.Object, configuration.Object, searchService.Object);

            // Act
            var result = v1Service.Search(null, null);

            // Assert
            Assert.Equal(1, result.Count());
            var package = result.First();
            Assert.Equal("Foo", package.Id);
            Assert.Equal("1.0.0", package.Version);
            Assert.Equal("http://test.nuget.org/packages/Foo/1.0.0", package.GalleryDetailsUrl);
            Assert.Equal("http://test.nuget.org/package/ReportAbuse/Foo/1.0.0", package.ReportAbuseUrl);
        }

        [Fact]
        public void V2FeedSearchDoesNotReturnPrereleasePackagesIfFlagIsFalse()
        {
            // Arrange
            var packageRegistration = new PackageRegistration { Id = "Foo" };
            var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            repo.Setup(r => r.GetAll()).Returns(new[] {
                new Package { PackageRegistration = packageRegistration, Version = "1.0.0", IsPrerelease = false, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
                new Package { PackageRegistration = packageRegistration, Version = "1.0.1-a", IsPrerelease = true, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
            }.AsQueryable());
            var searchService = new Mock<ISearchService>(MockBehavior.Strict);
            searchService.Setup(s => s.SearchWithRelevance(It.IsAny<IQueryable<Package>>(), It.IsAny<String>())).Returns<IQueryable<Package>, string>((_, __) => _);
            var configuration = new Mock<IConfiguration>(MockBehavior.Strict);
            configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://staged.nuget.org/");
            var v2Service = new TestableV2Feed(repo.Object, configuration.Object, searchService.Object);

            // Act
            var result = v2Service.Search(null, null, includePrerelease: false);

            // Assert
            Assert.Equal(1, result.Count());
            var package = result.First();
            Assert.Equal("Foo", package.Id);
            Assert.Equal("1.0.0", package.Version);
            Assert.Equal("https://staged.nuget.org/packages/Foo/1.0.0", package.GalleryDetailsUrl);
            Assert.Equal("https://staged.nuget.org/package/ReportAbuse/Foo/1.0.0", package.ReportAbuseUrl);
        }

        [Fact]
        public void V1FeedFindPackagesByIdReturnsUnlistedPackagesButNotPrereleasePackages()
        {
            // Arrange
            var packageRegistration = new PackageRegistration { Id = "Foo" };
            var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            repo.Setup(r => r.GetAll()).Returns(new[] {
                new Package { PackageRegistration = packageRegistration, Version = "1.0.0", IsPrerelease = false, Listed = false, DownloadStatistics = new List<PackageStatistics>() },
                new Package { PackageRegistration = packageRegistration, Version = "1.0.1-a", IsPrerelease = true, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
            }.AsQueryable());
            var configuration = new Mock<IConfiguration>(MockBehavior.Strict);
            configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
            var v1Service = new TestableV1Feed(repo.Object, configuration.Object, null);

            // Act
            var result = v1Service.FindPackagesById("Foo");

            // Assert
            Assert.Equal(1, result.Count());
            Assert.Equal("Foo", result.First().Id);
            Assert.Equal("1.0.0", result.First().Version);
            Assert.Equal("https://localhost:8081/packages/Foo/1.0.0", result.First().GalleryDetailsUrl);
        }

        [Fact]
        public void V2FeedFindPackagesByIdReturnsUnlistedAndPrereleasePackages()
        {
            // Arrange
            var packageRegistration = new PackageRegistration { Id = "Foo" };
            var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            repo.Setup(r => r.GetAll()).Returns(new[] {
                new Package { PackageRegistration = packageRegistration, Version = "1.0.0", IsPrerelease = false, Listed = false, DownloadStatistics = new List<PackageStatistics>() },
                new Package { PackageRegistration = packageRegistration, Version = "1.0.1-a", IsPrerelease = true, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
            }.AsQueryable());
            var configuration = new Mock<IConfiguration>(MockBehavior.Strict);
            configuration.Setup(c => c.GetSiteRoot(It.IsAny<bool>())).Returns("https://localhost:8081/");
            var v2Service = new TestableV2Feed(repo.Object, configuration.Object, null);

            // Act
            var result = v2Service.FindPackagesById("Foo");

            // Assert
            Assert.Equal(2, result.Count());
            Assert.Equal("Foo", result.First().Id);
            Assert.Equal("1.0.0", result.First().Version);

            Assert.Equal("Foo", result.Last().Id);
            Assert.Equal("1.0.1-a", result.Last().Version);
        }

        public class TestableV1Feed : V1Feed
        {
            public TestableV1Feed(
                IEntityRepository<Package> repo,
                IConfiguration configuration,
                ISearchService searchSvc)
                : base(repo, configuration, searchSvc)
            {
            }

            protected override bool UseHttps()
            {
                return false;
            }
        }

        public class TestableV2Feed : V2Feed
        {
            public TestableV2Feed(
                IEntityRepository<Package> repo,
                IConfiguration configuration,
                ISearchService searchSvc)
                : base(repo, configuration, searchSvc)
            {   
            }

            protected override bool UseHttps()
            {
                return false;
            }
        }
    }
}

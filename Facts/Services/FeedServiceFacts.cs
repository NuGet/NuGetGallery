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
                new Package { PackageRegistration = packageRegistration, Version = "1.0.1a", IsPrerelease = true, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
            }.AsQueryable());
            var v1Service = new V1Feed(repo.Object);

            // Act
            var result = v1Service.Search(null, null);

            // Assert
            Assert.Equal(1, result.Count());
            Assert.Equal("Foo", result.First().Id);
            Assert.Equal("1.0.0", result.First().Version);
        }

        [Fact]
        public void V1FeedSearchDoesNotReturnUnlistedPackages()
        {
            // Arrange
            var packageRegistration = new PackageRegistration { Id = "Foo" };
            var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            repo.Setup(r => r.GetAll()).Returns(new[] {
                new Package { PackageRegistration = packageRegistration, Version = "1.0.0", IsPrerelease = false, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
                new Package { PackageRegistration = packageRegistration, Version = "1.0.1a", IsPrerelease = true, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
                new Package { PackageRegistration = new PackageRegistration { Id ="baz" }, Version = "2.0", Listed = false, DownloadStatistics = new List<PackageStatistics>() },
            }.AsQueryable());
            var v1Service = new V1Feed(repo.Object);

            // Act
            var result = v1Service.Search(null, null);

            // Assert
            Assert.Equal(1, result.Count());
            Assert.Equal("Foo", result.First().Id);
            Assert.Equal("1.0.0", result.First().Version);
        }

        [Fact]
        public void V2FeedSearchDoesNotReturnPrereleasePackagesIfFlagIsFalse()
        {
            // Arrange
            var packageRegistration = new PackageRegistration { Id = "Foo" };
            var repo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
            repo.Setup(r => r.GetAll()).Returns(new[] {
                new Package { PackageRegistration = packageRegistration, Version = "1.0.0", IsPrerelease = false, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
                new Package { PackageRegistration = packageRegistration, Version = "1.0.1a", IsPrerelease = true, Listed = true, DownloadStatistics = new List<PackageStatistics>() },
            }.AsQueryable());
            var v2Service = new V2Feed(repo.Object);

            // Act
            var result = v2Service.Search(null, null, includePrerelease: false);

            // Assert
            Assert.Equal(1, result.Count());
            Assert.Equal("Foo", result.First().Id);
            Assert.Equal("1.0.0", result.First().Version);
        }
    }
}

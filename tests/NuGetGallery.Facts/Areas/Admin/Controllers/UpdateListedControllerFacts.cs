using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class UpdateListedControllerFacts
    {
        public class TheSearchAction : FactsBase
        {
            [Fact]
            public void QueriesForPackages()
            {
                // Arrange & Act
                var result = Target.Search(PackageRegistrationA.Id);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                var searchResults = Assert.IsType<List<PackageSearchResult>>(jsonResult.Data);
                Assert.Equal(3, searchResults.Count);
                Assert.Equal("4.3.0", searchResults[0].PackageVersionNormalized);
                Assert.Equal("4.4.0", searchResults[1].PackageVersionNormalized);
                Assert.Equal("4.5.0", searchResults[2].PackageVersionNormalized);
            }
        }

        public class TheUpdateListedAction : FactsBase
        {
            [Fact]
            public async Task ProcessesPackagesGroupedById()
            {
                // Arrange
                var input = new UpdateListedRequest
                {
                    Listed = false,
                    Packages = new List<string>
                    {
                        "NuGet.Versioning|9.9.9", // Does not exist
                        "NuGet.Versioning|4.3.0",
                        "NuGet.Versioning|4.4.0",
                        "NuGet.Frameworks|5.3.0",
                        "NuGet.Frameworks|5.4.0",
                    }
                };
                
                // Act
                var result = await Target.UpdateListed(input);

                // Assert
                PackageService.Verify(
                    x => x.FindPackagesById("NuGet.Versioning", PackageDeprecationFieldsToInclude.DeprecationAndRelationships),
                    Times.Once);
                PackageService.Verify(
                    x => x.FindPackagesById("NuGet.Frameworks", PackageDeprecationFieldsToInclude.DeprecationAndRelationships),
                    Times.Once);
                PackageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(It.Is<IReadOnlyList<Package>>(l => l.All(i => i.Id == "NuGet.Versioning")), false),
                    Times.Once);
                PackageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(It.Is<IReadOnlyList<Package>>(l => l.All(i => i.Id == "NuGet.Frameworks")), false),
                    Times.Once);
                Assert.Equal(
                    "4 packages across 2 package IDs have been unlisted. 1 packages were already up-to-date and were left unchanged.",
                    Target.TempData["Message"]);
            }

            [Theory]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.FailedValidation)]
            public async Task FiltersOutWrongPackageStatus(PackageStatus packageStatus)
            {
                // Arrange
                var input = new UpdateListedRequest
                {
                    Listed = false,
                    Packages = new List<string>
                    {
                        "NuGet.Versioning|4.4.0",
                        "NuGet.Versioning|4.3.0",
                    }
                };
                PackageRegistrationA.Packages.Single(x => x.NormalizedVersion == "4.3.0").PackageStatusKey = packageStatus;

                // Act
                var result = await Target.UpdateListed(input);

                // Assert
                PackageService.Verify(
                    x => x.FindPackagesById("NuGet.Versioning", PackageDeprecationFieldsToInclude.DeprecationAndRelationships),
                    Times.Once);
                PackageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(It.Is<IReadOnlyList<Package>>(l => l.Single().NormalizedVersion == "4.4.0"), false),
                    Times.Once);
                Assert.Equal(
                    "1 packages across 1 package IDs have been unlisted. 1 packages were already up-to-date and were left unchanged.",
                    Target.TempData["Message"]);
            }

            [Fact]
            public async Task FiltersOutPackagesWithMatchingListed()
            {
                // Arrange
                var input = new UpdateListedRequest
                {
                    Listed = false,
                    Packages = new List<string>
                    {
                        "NuGet.Versioning|4.4.0",
                        "NuGet.Versioning|4.3.0",
                    }
                };
                PackageRegistrationA.Packages.Single(x => x.NormalizedVersion == "4.3.0").Listed = false;

                // Act
                var result = await Target.UpdateListed(input);

                // Assert
                PackageService.Verify(
                    x => x.FindPackagesById("NuGet.Versioning", PackageDeprecationFieldsToInclude.DeprecationAndRelationships),
                    Times.Once);
                PackageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(It.Is<IReadOnlyList<Package>>(l => l.Single().NormalizedVersion == "4.4.0"), false),
                    Times.Once);
                Assert.Equal(
                    "1 packages across 1 package IDs have been unlisted. 1 packages were already up-to-date and were left unchanged.",
                    Target.TempData["Message"]);
            }

            [Fact]
            public async Task UsesPointQueryForSingleVersion()
            {
                // Arrange
                var input = new UpdateListedRequest
                {
                    Listed = false,
                    Packages = new List<string>
                    {
                        "NuGet.Versioning|4.4.0",
                    }
                };
                PackageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"))
                    .Returns(() => PackageRegistrationA.Packages.Single(x => x.NormalizedVersion == "4.4.0"));

                // Act
                var result = await Target.UpdateListed(input);

                // Assert
                PackageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"),
                    Times.Once);
                PackageUpdateService.Verify(
                    x => x.UpdateListedInBulkAsync(It.Is<IReadOnlyList<Package>>(l => l.Single().NormalizedVersion == "4.4.0"), false),
                    Times.Once);
                Assert.Equal(
                    "1 packages across 1 package IDs have been unlisted. 0 packages were already up-to-date and were left unchanged.",
                    Target.TempData["Message"]);
            }
        }

        public abstract class FactsBase : TestContainer
        {
            public FactsBase()
            {
                PackageService = new Mock<IPackageService>();
                PackageUpdateService = new Mock<IPackageUpdateService>();
                HttpContextBase = new Mock<HttpContextBase>();

                PackageRegistrationA = new PackageRegistration
                {
                    Id = "NuGet.Versioning",
                    Owners = new[]
                    {
                        new User { Username = "microsoft" },
                        new User { Username = "nuget" },
                    },
                };
                PackageRegistrationA.Packages = new List<Package>
                {
                    new Package
                    {
                        Key = 3,
                        NormalizedVersion = "4.5.0",
                        PackageRegistration = PackageRegistrationA,
                    },
                    new Package
                    {
                        Key = 2,
                        NormalizedVersion = "4.4.0",
                        PackageRegistration = PackageRegistrationA,
                    },
                    new Package
                    {
                        Key = 1,
                        NormalizedVersion = "4.3.0",
                        PackageRegistration = PackageRegistrationA,
                    },
                };
                PackageRegistrationB = new PackageRegistration
                {
                    Id = "NuGet.Frameworks",
                    Owners = new[]
                    {
                        new User { Username = "nuget" },
                    },
                };
                PackageRegistrationB.Packages = new List<Package>
                {
                    new Package
                    {
                        Key = 6,
                        NormalizedVersion = "5.5.0",
                        PackageRegistration = PackageRegistrationB,
                    },
                    new Package
                    {
                        Key = 5,
                        NormalizedVersion = "5.4.0",
                        PackageRegistration = PackageRegistrationB,
                    },
                    new Package
                    {
                        Key = 4,
                        NormalizedVersion = "5.3.0",
                        PackageRegistration = PackageRegistrationB,
                    },
                };

                PackageService
                    .Setup(x => x.FindPackageRegistrationById(PackageRegistrationA.Id))
                    .Returns(() => PackageRegistrationA);
                PackageService
                    .Setup(x => x.FindPackageRegistrationById(PackageRegistrationB.Id))
                    .Returns(() => PackageRegistrationB);
                PackageService
                    .Setup(x => x.FindPackagesById(PackageRegistrationA.Id, It.IsAny<PackageDeprecationFieldsToInclude>()))
                    .Returns(() => (IReadOnlyCollection<Package>)PackageRegistrationA.Packages);
                PackageService
                    .Setup(x => x.FindPackagesById(PackageRegistrationB.Id, It.IsAny<PackageDeprecationFieldsToInclude>()))
                    .Returns(() => (IReadOnlyCollection<Package>)PackageRegistrationB.Packages);

                Target = new UpdateListedController(PackageService.Object, PackageUpdateService.Object);
                TestUtility.SetupHttpContextMockForUrlGeneration(HttpContextBase, Target);
            }

            public Mock<IPackageService> PackageService { get; }
            public Mock<IPackageUpdateService> PackageUpdateService { get; }
            public Mock<HttpContextBase> HttpContextBase { get; }
            public PackageRegistration PackageRegistrationA { get; }
            public string Query { get; set; }
            public PackageRegistration PackageRegistrationB { get; }
            public UpdateListedController Target { get; }
        }
    }
}

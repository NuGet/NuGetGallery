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
            public async Task CallsServiceWithParsedPackageIdentities()
            {
                // Arrange
                var input = new UpdateListedRequest
                {
                    Listed = false,
                    Packages =
                    [
                        "NuGet.Versioning|4.3.0",
                        "NuGet.Versioning|4.4.0",
                        "NuGet.Frameworks|5.3.0",
                    ]
                };

                UpdateListedService
                    .Setup(x => x.UpdateListedAsync(
                        It.IsAny<IReadOnlyList<UpdateListedPackageIdentity>>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .ReturnsAsync(
                    [
                        new UpdateListedPackageResult { Id = "NuGet.Versioning", Version = "4.3.0", Result = UpdateListedServiceResult.Success },
                        new UpdateListedPackageResult { Id = "NuGet.Versioning", Version = "4.4.0", Result = UpdateListedServiceResult.Success },
                        new UpdateListedPackageResult { Id = "NuGet.Frameworks", Version = "5.3.0", Result = UpdateListedServiceResult.Success },
                    ]);

                // Act
                var result = await Target.UpdateListed(input);

                // Assert
                UpdateListedService.Verify(
                    x => x.UpdateListedAsync(
                        It.Is<IReadOnlyList<UpdateListedPackageIdentity>>(p => p.Count == 3),
                        false,
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Once);
                Assert.Equal(
                    "3 packages across 2 package IDs have been unlisted. 0 packages were skipped because they are deleted or they failed validation.",
                    Target.TempData["Message"]);
            }

            [Fact]
            public async Task CountsNotFoundPackagesAsSkipped()
            {
                // Arrange
                var input = new UpdateListedRequest
                {
                    Listed = false,
                    Packages =
                    [
                        "NuGet.Versioning|4.3.0",
                        "NuGet.Versioning|9.9.9",
                    ]
                };

                UpdateListedService
                    .Setup(x => x.UpdateListedAsync(
                        It.IsAny<IReadOnlyList<UpdateListedPackageIdentity>>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .ReturnsAsync(
                    [
                        new UpdateListedPackageResult { Id = "NuGet.Versioning", Version = "4.3.0", Result = UpdateListedServiceResult.Success },
                        new UpdateListedPackageResult { Id = "NuGet.Versioning", Version = "9.9.9", Result = UpdateListedServiceResult.PackageNotFound },
                    ]);

                // Act
                var result = await Target.UpdateListed(input);

                // Assert
                Assert.Equal(
                    "1 packages across 1 package IDs have been unlisted. 1 packages were skipped because they are deleted or they failed validation.",
                    Target.TempData["Message"]);
            }

            [Fact]
            public async Task SkipsMalformedEntries()
            {
                // Arrange
                var input = new UpdateListedRequest
                {
                    Listed = true,
                    Packages =
                    [
                        "NuGet.Versioning|4.4.0",
                        "malformed-no-pipe",
                    ]
                };

                UpdateListedService
                    .Setup(x => x.UpdateListedAsync(
                        It.IsAny<IReadOnlyList<UpdateListedPackageIdentity>>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .ReturnsAsync(
                    [
                        new UpdateListedPackageResult { Id = "NuGet.Versioning", Version = "4.4.0", Result = UpdateListedServiceResult.Success },
                    ]);

                // Act
                var result = await Target.UpdateListed(input);

                // Assert
                UpdateListedService.Verify(
                    x => x.UpdateListedAsync(
                        It.Is<IReadOnlyList<UpdateListedPackageIdentity>>(p => p.Count == 1 && p[0].Id == "NuGet.Versioning"),
                        true,
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public async Task RedirectsToIndexAfterUpdate()
            {
                // Arrange
                var input = new UpdateListedRequest
                {
                    Listed = false,
                    Packages =
                    [
                        "NuGet.Versioning|4.4.0",
                    ]
                };

                UpdateListedService
                    .Setup(x => x.UpdateListedAsync(
                        It.IsAny<IReadOnlyList<UpdateListedPackageIdentity>>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .ReturnsAsync(
                    [
                        new UpdateListedPackageResult { Id = "NuGet.Versioning", Version = "4.4.0", Result = UpdateListedServiceResult.Success },
                    ]);

                // Act
                var result = await Target.UpdateListed(input);

                // Assert
                var redirect = Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("Index", redirect.RouteValues["action"]);
            }
        }

        public abstract class FactsBase : TestContainer
        {
            public FactsBase()
            {
                PackageService = new Mock<IPackageService>();
                UpdateListedService = new Mock<IUpdateListedService>();
                HttpContextBase = new Mock<HttpContextBase>();

                PackageRegistrationA = new PackageRegistration
                {
                    Id = "NuGet.Versioning",
                    Owners =
                    [
                        new User { Username = "microsoft" },
                        new User { Username = "nuget" },
                    ],
                };
                PackageRegistrationA.Packages =
                [
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
                ];

                PackageService
                    .Setup(x => x.FindPackageRegistrationById(PackageRegistrationA.Id))
                    .Returns(() => PackageRegistrationA);

                Target = new UpdateListedController(PackageService.Object, UpdateListedService.Object);
                TestUtility.SetupHttpContextMockForUrlGeneration(HttpContextBase, Target);
            }

            public Mock<IPackageService> PackageService { get; }
            public Mock<IUpdateListedService> UpdateListedService { get; }
            public Mock<HttpContextBase> HttpContextBase { get; }
            public PackageRegistration PackageRegistrationA { get; }
            public UpdateListedController Target { get; }
        }
    }
}

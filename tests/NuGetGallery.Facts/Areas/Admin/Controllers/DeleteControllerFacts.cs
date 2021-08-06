// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class DeleteControllerFacts
    {
        public class TheSearchMethod : FactsBase
        {
            [Fact]
            public void DoesNotMakeDuplicateQueries()
            {
                // Arrange
                _query = "NuGet.Versioning" + Environment.NewLine + "  NUGET.VERSIONING   ";

                // Act
                var result = _target.Search(_query);

                // Assert
                _packageService.Verify(
                    x => x.FindPackageRegistrationById("NuGet.Versioning"),
                    Times.Once);
            }

            [Fact]
            public void DoesNotReturnDuplicatePackages()
            {
                // Arrange
                _packages.Add(_packages[0]);

                // Act
                var result = _target.Search(_query);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                var searchResults = Assert.IsType<List<PackageSearchResult>>(jsonResult.Data);
                var uniqueIdentities = searchResults.Select(p => $"{p.PackageId} {p.PackageVersionNormalized}").Distinct();
                Assert.Equal(searchResults.Count, uniqueIdentities.Count());
            }

            [Fact]
            public void UsesVersionSpecificIdIfAvailable()
            {
                // Arrange
                _packages[0].Id = "nuget.versioning";

                // Act
                var result = _target.Search(_query);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                var searchResults = Assert.IsType<List<PackageSearchResult>>(jsonResult.Data);
                Assert.Equal("NuGet.Versioning", searchResults[0].PackageId);
                Assert.Equal("4.3.0", searchResults[0].PackageVersionNormalized);
                Assert.Equal("nuget.versioning", searchResults[1].PackageId);
                Assert.Equal("4.4.0", searchResults[1].PackageVersionNormalized);
            }
        }

        public abstract class FactsBase : TestContainer
        {
            protected string _query;
            protected readonly List<Package> _packages;
            protected readonly Mock<IPackageService> _packageService;
            protected readonly Mock<IPackageDeleteService> _packageDeleteService;
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly Mock<HttpContextBase> _httpContextBase;
            protected readonly DeleteController _target;

            public FactsBase()
            {
                _query = "NuGet.Versioning";
                _packages = new List<Package>()
                {
                    new Package
                    {
                        Key = 2,
                        PackageRegistration = new PackageRegistration
                        {
                            Id = "NuGet.Versioning",
                            Owners = new[]
                            {
                                new User { Username = "microsoft" },
                                new User { Username = "nuget" },
                            }
                        },
                        NormalizedVersion = "4.4.0",
                    },
                    new Package
                    {
                        Key = 1,
                        PackageRegistration = new PackageRegistration
                        {
                            Id = "NuGet.Versioning",
                            Owners = new[]
                            {
                                new User { Username = "microsoft" },
                                new User { Username = "nuget" },
                            }
                        },
                        NormalizedVersion = "4.3.0",
                    },
                };

                _packageService = new Mock<IPackageService>();
                _packageService
                    .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(() => new PackageRegistration { Packages = _packages });

                _packageDeleteService = new Mock<IPackageDeleteService>();

                _telemetryService = new Mock<ITelemetryService>();

                _httpContextBase = new Mock<HttpContextBase>();

                _target = new DeleteController(_packageService.Object, _packageDeleteService.Object, _telemetryService.Object);

                TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, _target);
            }
        }
    }
}

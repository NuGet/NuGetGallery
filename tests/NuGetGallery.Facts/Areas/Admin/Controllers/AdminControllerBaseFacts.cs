// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class AdminControllerBaseFacts
    {
        public class TheSearchForPackagesMethod : FactsBase
        {
            [Fact]
            public void DoesNotMakeDuplicateIdQueries()
            {
                // Arrange
                Query = "NuGet.Versioning" + Environment.NewLine + "  NUGET.VERSIONING   ";

                // Act
                var result = Target.SearchForPackages(PackageService.Object, Query);

                // Assert
                PackageService.Verify(
                    x => x.FindPackageRegistrationById("NuGet.Versioning"),
                    Times.Once);
            }

            [Fact]
            public void DoesNotMakeDuplicateVersionQueries()
            {
                // Arrange
                Query = "NuGet.Versioning 4.3.0" + Environment.NewLine + "  NUGET.VERSIONING  4.3.0 ";

                // Act
                var result = Target.SearchForPackages(PackageService.Object, Query);

                // Assert
                PackageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.3.0"),
                    Times.Once);
            }

            [Fact]
            public void CanSearchForSpecificVersionSplitBySlash()
            {
                // Arrange
                Query = "NuGet.Versioning/4.3.0\nNuGet.VERSIONING/4.4.0";

                // Act
                var result = Target.SearchForPackages(PackageService.Object, Query);

                // Assert
                Assert.Equal(2, result.Count);
                PackageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.3.0"),
                    Times.Once);
                PackageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict("NuGet.VERSIONING", "4.4.0"),
                    Times.Once);
            }

            [Fact]
            public void SplitsQueriesByComma()
            {
                // Arrange
                Query = "NuGet.Versioning/4.3.0,NuGet.Versioning/4.4.0";

                // Act
                var result = Target.SearchForPackages(PackageService.Object, Query);

                // Assert
                Assert.Equal(2, result.Count);
                PackageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.3.0"),
                    Times.Once);
                PackageService.Verify(
                    x => x.FindPackageByIdAndVersionStrict("NuGet.Versioning", "4.4.0"),
                    Times.Once);
            }

            [Fact]
            public void DoesNotReturnDuplicatePackages()
            {
                // Arrange
                Packages.Add(Packages[0]);

                // Act
                var result = Target.SearchForPackages(PackageService.Object, Query);

                // Assert
                Assert.Equal(2, result.Count);
                Assert.Equal("4.3.0", result[0].NormalizedVersion);
                Assert.Equal("4.4.0", result[1].NormalizedVersion);
            }

            [Fact]
            public void UsesVersionSpecificIdIfAvailable()
            {
                // Arrange
                Packages[0].Id = "nuget.versioning";

                // Act
                var result = Target.SearchForPackages(PackageService.Object, Query);

                // Assert
                Assert.Equal("NuGet.Versioning", result[0].PackageRegistration.Id);
                Assert.Equal("4.3.0", result[0].NormalizedVersion);
                Assert.Equal("NuGet.Versioning", result[1].PackageRegistration.Id);
                Assert.Equal("4.4.0", result[1].NormalizedVersion);
            }
        }

        public class FactsBase
        {
            public FactsBase()
            {
                PackageService = new Mock<IPackageService>();

                Query = "NuGet.Versioning";
                var packageRegistration = new PackageRegistration
                {
                    Id = "NuGet.Versioning",
                    Owners = new[]
                    {
                        new User { Username = "microsoft" },
                        new User { Username = "nuget" },
                    }
                };
                Packages = new List<Package>()
                {
                    new Package
                    {
                        Key = 2,
                        PackageRegistration = packageRegistration,
                        NormalizedVersion = "4.4.0",
                    },
                    new Package
                    {
                        Key = 1,
                        PackageRegistration = packageRegistration,
                        NormalizedVersion = "4.3.0",
                    },
                };

                PackageService
                    .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns<string>(x => new PackageRegistration
                    {
                        Packages = Packages.Where(y => y.PackageRegistration.Id.Equals(x, StringComparison.OrdinalIgnoreCase)).ToList(),
                    });

                PackageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns<string, string>((i, v) => Packages
                        .Where(x =>
                            x.PackageRegistration.Id.Equals(i, StringComparison.OrdinalIgnoreCase) &&
                            x.NormalizedVersion.Equals(NuGetVersionFormatter.Normalize(v), StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault());

                Target = new AdminControllerBase();
            }

            public Mock<IPackageService> PackageService { get; }
            public string Query { get; set; }
            public List<Package> Packages { get; }
            public AdminControllerBase Target { get; }
        }
    }
}

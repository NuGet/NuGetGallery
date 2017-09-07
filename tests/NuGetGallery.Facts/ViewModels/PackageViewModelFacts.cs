// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class PackageViewModelFacts
    {
        [Fact]
        public void UsesNormalizedVersionForDisplay()
        {
            var package = new Package()
            {
                Version = "01.02.00.00",
                NormalizedVersion = "1.3.0" // Different just to prove the View Model is using the DB column.
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.Equal("1.3.0", packageViewModel.Version);
        }

        [Fact]
        public void UsesNormalizedPackageVersionIfNormalizedVersionMissing()
        {
            var package = new Package()
            {
                Version = "01.02.00.00"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.Equal("1.2.0", packageViewModel.Version);
        }

        [Fact]
        public void LicenseNamesAreParsedByCommas()
        {
            var package = new Package
            {
                Version = "1.0.0",
                LicenseNames = "l1,l2, l3 ,l4  ,  l5 ",
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.Equal(new string[] { "l1", "l2", "l3", "l4", "l5" }, packageViewModel.LicenseNames);
        }

        [Fact]
        public void LicenseReportFieldsKeptWhenLicenseReportDisabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = true,
                LicenseNames = "l1",
                LicenseReportUrl = "url"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.NotNull(packageViewModel.LicenseNames);
            Assert.NotNull(packageViewModel.LicenseReportUrl);
        }

        [Fact]
        public void LicenseReportUrlKeptWhenLicenseReportEnabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = false,
                LicenseReportUrl = "url"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.NotNull(packageViewModel.LicenseReportUrl);
        }

        [Fact]
        public void LicenseNamesKeptWhenLicenseReportEnabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = false,
                LicenseNames = "l1"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.NotNull(packageViewModel.LicenseNames);
        }

        [Fact]
        public void LicenseUrlKeptWhenLicenseReportDisabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = true,
                LicenseUrl = "url"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.NotNull(packageViewModel.LicenseUrl);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void HasSemVer2DependencyIsFalseWhenInvalidDependencyVersionRange(string versionSpec)
        {
            // Arrange
            var package = new Package
            {
                Version = "1.0.0",
                Dependencies = new List<PackageDependency>
                {
                    new PackageDependency { VersionSpec = versionSpec}
                }
            };

            // Act
            var viewModel = new PackageViewModel(package);

            // Assert
            Assert.False(viewModel.HasSemVer2Dependency);
        }

        [Theory]
        [InlineData("2.0.0-alpha.1")]
        [InlineData("2.0.0+metadata")]
        [InlineData("2.0.0-alpha+metadata")]
        public void HasSemVer2DependencyWhenSemVer2DependencyVersionSpec(string versionSpec)
        {
            // Arrange
            var package = new Package
            {
                Version = "1.0.0",
                Dependencies = new List<PackageDependency>
                {
                    new PackageDependency { VersionSpec = versionSpec}
                }
            };

            // Act
            var viewModel = new PackageViewModel(package);

            // Assert
            Assert.True(viewModel.HasSemVer2Dependency);
        }

        [Theory]
        [InlineData("2.0.0-alpha")]
        [InlineData("2.0.0")]
        [InlineData("2.0.0.0")]
        public void HasSemVer2DependencyIsFalseWhenNonSemVer2DependencyVersionSpec(string versionSpec)
        {
            // Arrange
            var package = new Package
            {
                Version = "1.0.0",
                Dependencies = new List<PackageDependency>
                {
                    new PackageDependency { VersionSpec = versionSpec}
                }
            };

            // Act
            var viewModel = new PackageViewModel(package);

            // Assert
            Assert.False(viewModel.HasSemVer2Dependency);
        }
        
        [Theory]
        [InlineData("2.0.0-alpha")]
        [InlineData("2.0.0")]
        [InlineData("2.0.0.0")]
        public void HasSemVer2VersionIsFalseWhenNonSemVer2Version(string version)
        {
            // Arrange
            var package = new Package
            {
                Version = version
            };

            // Act
            var viewModel = new PackageViewModel(package);

            // Assert
            Assert.False(viewModel.HasSemVer2Version);
        }

        [Theory]
        [InlineData("2.0.0-alpha.1")]
        [InlineData("2.0.0+metadata")]
        [InlineData("2.0.0-alpha+metadata")]
        public void HasSemVer2VersionIsTrueWhenSemVer2Version(string version)
        {
            // Arrange
            var package = new Package
            {
                Version = version
            };

            // Act
            var viewModel = new PackageViewModel(package);

            // Assert
            Assert.True(viewModel.HasSemVer2Version);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Versioning;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class DisplayPackageViewModelFacts
    {
        [Fact]
        public void TheCtorSortsPackageVersionsProperly()
        {
            var package = new Package
                {
                    Dependencies = Enumerable.Empty<PackageDependency>().ToList(),
                    PackageRegistration = new PackageRegistration
                        {
                            Owners = Enumerable.Empty<User>().ToList(),
                        }
                };

            package.PackageRegistration.Packages = new[]
                {
                    new Package { Version = "1.0.0-alpha2", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.0", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.0-alpha", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.0-beta", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.2-beta", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.2", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.10", PackageRegistration = package.PackageRegistration }
                };

            var packageVersions = new DisplayPackageViewModel(package, package.PackageRegistration.Packages.OrderByDescending(p => new NuGetVersion(p.Version)))
                .PackageVersions.ToList();

            // Descending
            Assert.Equal("1.0.0-alpha", packageVersions[6].Version);
            Assert.Equal("1.0.0-alpha2", packageVersions[5].Version);
            Assert.Equal("1.0.0-beta", packageVersions[4].Version);
            Assert.Equal("1.0.0", packageVersions[3].Version);
            Assert.Equal("1.0.2-beta", packageVersions[2].Version);
            Assert.Equal("1.0.2", packageVersions[1].Version);
            Assert.Equal("1.0.10", packageVersions[0].Version);
        }

        [Fact]
        public void DownloadsPerDayLabelShowsLessThanOneWhenAverageBelowOne()
        {
            // Arrange
            const int downloadCount = 10;
            const int daysSinceCreated = 11;

            var package = new Package
            {
                Dependencies = Enumerable.Empty<PackageDependency>().ToList(),
                DownloadCount = downloadCount,
                PackageRegistration = new PackageRegistration
                {
                    Owners = Enumerable.Empty<User>().ToList(),
                    DownloadCount = downloadCount
                },
                Created = DateTime.UtcNow.AddDays(-daysSinceCreated),
                Version = "1.0.10"
            };

            package.PackageRegistration.Packages = new[] { package };

            var viewModel = new DisplayPackageViewModel(package, package.PackageRegistration.Packages.OrderByDescending(p => new NuGetVersion(p.Version)));

            // Act
            var label = viewModel.DownloadsPerDayLabel;

            // Assert
            Assert.Equal("<1", label);
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(11, 10)]
        [InlineData(14, 10)]
        [InlineData(15, 10)]
        public void DownloadsPerDayLabelShowsOneWhenAverageBetweenOneAndOnePointFive(int downloadCount, int daysSinceCreated)
        {
            // Arrange
            var package = new Package
            {
                Dependencies = Enumerable.Empty<PackageDependency>().ToList(),
                DownloadCount = downloadCount,
                PackageRegistration = new PackageRegistration
                {
                    Owners = Enumerable.Empty<User>().ToList(),
                    DownloadCount = downloadCount
                },
                Created = DateTime.UtcNow.AddDays(-daysSinceCreated),
                Version = "1.0.10"
            };

            package.PackageRegistration.Packages = new[] { package };

            var viewModel = new DisplayPackageViewModel(package, package.PackageRegistration.Packages.OrderByDescending(p => new NuGetVersion(p.Version)));

            // Act
            var label = viewModel.DownloadsPerDayLabel;

            // Assert
            Assert.Equal("1", label);
        }
    }
}
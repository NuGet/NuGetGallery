// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;
using NuGetGallery.Framework;
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
                Version = "1.0.0",
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

            var packageVersions = new DisplayPackageViewModel(package, null, package.PackageRegistration.Packages.OrderByDescending(p => new NuGetVersion(p.Version)))
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
        public void AvgDownloadsPerDayConsidersOldestPackageVersionInHistory()
        {
            // Arrange
            var utcNow = DateTime.UtcNow;
            const int daysSinceFirstPackageCreated = 10;
            const int totalDownloadCount = 250;

            var packageRegistration = new PackageRegistration
            {
                Owners = Enumerable.Empty<User>().ToList(),
                DownloadCount = totalDownloadCount
            };

            var package = new Package
            {
                // Simulating that lowest package version was pushed latest, on-purpose, 
                // to assert we use the *oldest* package version in the calculation.
                Created = utcNow,
                Dependencies = Enumerable.Empty<PackageDependency>().ToList(),
                DownloadCount = 10,
                PackageRegistration = packageRegistration,
                Version = "1.0.0"
            };

            package.PackageRegistration.Packages = new[]
                {
                    package,
                    new Package { Version = "1.0.1", PackageRegistration = packageRegistration, DownloadCount = 100, Created = utcNow.AddDays(-daysSinceFirstPackageCreated) },
                    new Package { Version = "2.0.1", PackageRegistration = packageRegistration, DownloadCount = 140, Created = utcNow.AddDays(-3) }
                };

            var packageHistory = packageRegistration.Packages.OrderByDescending(p => new NuGetVersion(p.Version));

            // Act
            var viewModel = new DisplayPackageViewModel(package, null, packageHistory);

            // Assert
            Assert.Equal(daysSinceFirstPackageCreated, viewModel.TotalDaysSinceCreated);
            Assert.Equal(totalDownloadCount / daysSinceFirstPackageCreated, viewModel.DownloadsPerDay);
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

            var viewModel = new DisplayPackageViewModel(package, null, package.PackageRegistration.Packages.OrderByDescending(p => new NuGetVersion(p.Version)));

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

            var viewModel = new DisplayPackageViewModel(package, null, package.PackageRegistration.Packages.OrderByDescending(p => new NuGetVersion(p.Version)));

            // Act
            var label = viewModel.DownloadsPerDayLabel;

            // Assert
            Assert.Equal("1", label);
        }

        [Theory]
        [InlineData("1.0.0", "1.0.1-alpha", true)]
        [InlineData("1.0.0", "1.0.1-alpha+metadata", true)]
        [InlineData("1.0.0", "1.0.1-alpha.1", true)]
        [InlineData("1.0.0", "1.0.1", false)]
        [InlineData("1.0.0", "1.0.0-alpha", false)]
        [InlineData("1.0.0", "1.0.0-alpha+metadata", false)]
        [InlineData("1.0.0", "1.0.0-alpha.1", false)]
        [InlineData("1.0.0-alpha", "1.0.0-alpha.1", true)]
        public void HasNewerPrereleaseReturnsTrueWhenNewerPrereleaseAvailable(
            string currentVersion, 
            string otherVersion, 
            bool expectedNewerPrereleaseAvailable)
        {
            // Arrange
            var dependencies = Enumerable.Empty<PackageDependency>().ToList();
            var packageRegistration = new PackageRegistration
            {
                Owners = Enumerable.Empty<User>().ToList(),
            };

            var package = new Package
            {
                Dependencies = dependencies,
                PackageRegistration = packageRegistration,
                IsPrerelease = NuGetVersion.Parse(currentVersion).IsPrerelease,
                Version = currentVersion
            };

            var otherPackage = new Package
            {
                Dependencies = dependencies,
                PackageRegistration = packageRegistration,
                IsPrerelease = NuGetVersion.Parse(otherVersion).IsPrerelease,
                Version = otherVersion
            };

            package.PackageRegistration.Packages = new[] { package, otherPackage };

            var viewModel = new DisplayPackageViewModel(package, null, package.PackageRegistration.Packages.OrderByDescending(p => new NuGetVersion(p.Version)));

            // Act
            var hasNewerPrerelease = viewModel.HasNewerPrerelease;

            // Assert
            Assert.Equal(expectedNewerPrereleaseAvailable, hasNewerPrerelease);
        }
        
        [Fact]
        public void HasNewerPrereleaseDoesNotConsiderUnlistedVersions()
        {
            // Arrange
            var dependencies = Enumerable.Empty<PackageDependency>().ToList();
            var packageRegistration = new PackageRegistration
            {
                Owners = Enumerable.Empty<User>().ToList(),
            };

            var package = new Package
            {
                Dependencies = dependencies,
                PackageRegistration = packageRegistration,
                IsPrerelease = true,
                Version = "1.0.0-alpha.1"
            };

            // This is a newer prerelease version, however unlisted.
            var otherPackage = new Package
            {
                Dependencies = dependencies,
                PackageRegistration = packageRegistration,
                IsPrerelease = true,
                Version = "1.0.0-alpha.2",
                Listed = false
            };

            package.PackageRegistration.Packages = new[] { package, otherPackage };

            var viewModel = new DisplayPackageViewModel(package, null, package.PackageRegistration.Packages.OrderByDescending(p => new NuGetVersion(p.Version)));

            // Act
            var hasNewerPrerelease = viewModel.HasNewerPrerelease;

            // Assert
            Assert.False(hasNewerPrerelease);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void HasSemVer2DependencyIsFalseWhenInvalidDependencyVersionRange(string versionSpec)
        {
            // Arrange
            var package = CreateTestPackage("1.0.0", dependencyVersion: versionSpec);
            var history = package.PackageRegistration.Packages.OrderByDescending(p => p.Version);

            // Act
            var viewModel = new DisplayPackageViewModel(package, null, history);

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
            var package = CreateTestPackage("1.0.0", dependencyVersion: versionSpec);
            var history = package.PackageRegistration.Packages.OrderByDescending(p => p.Version);

            // Act
            var viewModel = new DisplayPackageViewModel(package, null, history);

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
            var package = CreateTestPackage("1.0.0", dependencyVersion: versionSpec);
            var history = package.PackageRegistration.Packages.OrderByDescending(p => p.Version);

            // Act
            var viewModel = new DisplayPackageViewModel(package, null, history);

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
            var package = CreateTestPackage(version);
            var history = package.PackageRegistration.Packages.OrderByDescending(p => p.Version);

            // Act
            var viewModel = new DisplayPackageViewModel(package, null, history);

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
            var package = CreateTestPackage(version);
            var history = package.PackageRegistration.Packages.OrderByDescending(p => p.Version);

            // Act
            var viewModel = new DisplayPackageViewModel(package, null, history);

            // Assert
            Assert.True(viewModel.HasSemVer2Version);
        }

        private Package CreateTestPackage(string version, string dependencyVersion = null)
        {
            var package = new Package
            {
                Version = version,
                PackageRegistration = new PackageRegistration
                {
                    Owners = Enumerable.Empty<User>().ToList(),
                }
            };
            if (!string.IsNullOrEmpty(dependencyVersion))
            {
                package.Dependencies = new List<PackageDependency>
                {
                    new PackageDependency { VersionSpec = dependencyVersion }
                };
            }
            package.PackageRegistration.Packages = new[] { package };
            return package;
        }

        public class ThePushedByField
        {
            public enum UserPackageOwnerState
            {
                HasNoUserPackageOwner,
                HasUserPackageOwner,
                CurrentUserIsUserPackageOwner
            }

            public enum OrganizationPackageOwnerState
            {
                HasNoOrganizationPackageOwner,
                HasOrganizationPackageOwner,
                CurrentUserIsMemberOfOrganizationPackageOwner
            }

            public static IEnumerable<object[]> Data
            {
                get
                {
                    foreach (var userPackageOwnerState in Enum.GetValues(typeof(UserPackageOwnerState)).Cast<UserPackageOwnerState>())
                    {
                        foreach (var organizationPackageOwnerState in Enum.GetValues(typeof(OrganizationPackageOwnerState)).Cast<OrganizationPackageOwnerState>())
                        {
                            var key = 0;
                            var currentUser = new User("currentUser") { Key = key++ };
                            var owners = new List<User>();

                            User packageOwner = null;
                            if (userPackageOwnerState != UserPackageOwnerState.HasNoUserPackageOwner)
                            {
                                packageOwner = userPackageOwnerState == UserPackageOwnerState.CurrentUserIsUserPackageOwner ? currentUser : new User("packageOwner") { Key = key++ };
                                owners.Add(packageOwner);
                            }

                            Organization organizationOwner = new Organization("organizationOwner") { Key = key++ };
                            User organizationMember = null;
                            if (organizationPackageOwnerState != OrganizationPackageOwnerState.HasNoOrganizationPackageOwner)
                            {
                                organizationMember = organizationPackageOwnerState == OrganizationPackageOwnerState.CurrentUserIsMemberOfOrganizationPackageOwner ? currentUser : new User("organizationMember") { Key = key++ };
                                owners.Add(organizationMember);
                            }

                            var canViewPrivateMetadata =
                                userPackageOwnerState == UserPackageOwnerState.CurrentUserIsUserPackageOwner ||
                                organizationPackageOwnerState == OrganizationPackageOwnerState.CurrentUserIsMemberOfOrganizationPackageOwner;

                            var packageRegistration = new PackageRegistration { Owners = owners };

                            var packageWithoutUser = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                            yield return MemberDataHelper.AsData(packageWithoutUser, currentUser, null);

                            var userThatIsNotOwner = new User("notAnOwner") { Key = key++ };
                            var packageWithUserThatIsNotOwner = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", User = userThatIsNotOwner };
                            yield return MemberDataHelper.AsData(packageWithUserThatIsNotOwner, currentUser, canViewPrivateMetadata ? userThatIsNotOwner.Username : null);

                            if (userPackageOwnerState != UserPackageOwnerState.HasNoUserPackageOwner)
                            {
                                var packageWithUserUser = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", User = packageOwner };
                                yield return MemberDataHelper.AsData(packageWithUserUser, currentUser, canViewPrivateMetadata ? packageWithUserUser.User.Username : null);
                            }

                            if (organizationPackageOwnerState != OrganizationPackageOwnerState.CurrentUserIsMemberOfOrganizationPackageOwner)
                            {
                                var packageWithOrganizationUser = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", User = organizationOwner };

                                string expected = canViewPrivateMetadata ?
                                    (organizationPackageOwnerState == OrganizationPackageOwnerState.CurrentUserIsMemberOfOrganizationPackageOwner ?
                                        organizationMember.Username :
                                        organizationOwner.Username) :
                                    null;

                                yield return MemberDataHelper.AsData(packageWithOrganizationUser, currentUser, expected);
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(Data))]
            public void ReturnsExpectedUser(Package package, User currentUser, string expected)
            {
                var model = new DisplayPackageViewModel(package, currentUser, new[] { package }.OrderByDescending(p => new NuGetVersion(p.Version)));

                Assert.Equal(expected, model.PushedBy);
            }
        }
    }
}
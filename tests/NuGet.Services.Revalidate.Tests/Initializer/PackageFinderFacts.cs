// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;
using Tests.ContextHelpers;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Initializer
{
    public class PackageFinderFacts
    {
        public class TheFindMicrosoftPackagesMethod : FactsBase
        {
            [Fact]
            public void FindsPackagesWithMicrosoftAsCoOwner()
            {
                // Arrange
                _context.Mock(packageRegistrations: new[]
                {
                    new PackageRegistration
                    {
                        Key = 1,
                        Owners = new[]
                        {
                            new User { Username = "Microsoft" }
                        }
                    },
                    new PackageRegistration
                    {
                        Key = 2,
                        Owners = new[]
                        {
                            new User { Username = "Billy Bob" }
                        }
                    },
                    new PackageRegistration
                    {
                        Key = 3,
                        Owners = new[]
                        {
                            new User { Username = "Billy Bob" },
                            new User { Username = "Microsoft" }
                        }
                    }
                });

                var expected = new HashSet<int> { 1, 3 };

                // Act & assert
                var actual = _target.FindMicrosoftPackages();

                Assert.Equal(expected, actual);
            }
        }

        public class TheFindPreinstalledPackagesMethod : FactsBase
        {
            public TheFindPreinstalledPackagesMethod()
            {
                // This assumes a Windows machine with Visual Studio installed!
                _config.PreinstalledPaths = new List<string>
                {
                    "C:\\Program Files (x86)\\Microsoft SDKs\\NuGetPackages"
                };
            }

            [Fact]
            public void FindsPreinstalledPackages()
            {
                _context.Mock(packageRegistrations: new[]
                {
                    new PackageRegistration { Key = 1, Id = "system.linq" },
                    new PackageRegistration { Key = 2, Id = "newtonsoft.json" }
                });

                var actual = _target.FindPreinstalledPackages(except: new HashSet<int>());

                Assert.Contains(1, actual);
                Assert.Contains(2, actual);
            }

            [Fact]
            public void SkipsPackagesInExceptSet()
            {
                _context.Mock(packageRegistrations: new[]
                {
                    new PackageRegistration { Key = 1, Id = "system.linq" },
                    new PackageRegistration { Key = 2, Id = "newtonsoft.json" }
                });

                var actual = _target.FindPreinstalledPackages(except: new HashSet<int> { 1 });

                Assert.DoesNotContain(1, actual);
                Assert.Contains(2, actual);
            }

            [Fact]
            public void SkipsPackagesWithNoRegistration()
            {
                _context.Mock(packageRegistrations: new[]
                {
                    new PackageRegistration { Key = 1, Id = "system.linq" },
                });

                var actual = _target.FindPreinstalledPackages(except: new HashSet<int>());

                Assert.Contains(1, actual);
                Assert.DoesNotContain(2, actual);
            }
        }

        public class TheFindDependencyPackagesMethod : FactsBase
        {
            [Fact]
            public void FindsAllDependencies()
            {
                // Registrations 1 & 2 are the root set.
                // Package 1 depends on 3, which depends on package 4, which depends on 2
                // Package 5 is unused
                // Package 2 depends on 6
                _context.Mock(
                    packageRegistrations: new[]
                    {
                        new PackageRegistration { Key = 1, Id = "1" },
                        new PackageRegistration { Key = 2, Id = "2" },
                        new PackageRegistration { Key = 3, Id = "3" },
                        new PackageRegistration { Key = 4, Id = "4" },
                        new PackageRegistration { Key = 5, Id = "5" },
                        new PackageRegistration { Key = 6, Id = "6" },
                    },
                    packageDependencies: new[]
                    {
                        MakeDependency(1, dependsOn: "3"),
                        MakeDependency(3, dependsOn: "4"),
                        MakeDependency(4, dependsOn: "2"),
                        MakeDependency(2, dependsOn: "6"),
                    });

                var expected = new HashSet<int> { 3, 4, 6 };

                // Act & assert
                var actual = _target.FindDependencyPackages(new HashSet<int> { 1, 2 });

                Assert.Equal(expected, actual);
            }

            private PackageDependency MakeDependency(int registrationKey, string dependsOn)
            {
                return new PackageDependency
                {
                    // The package that owns this dependency
                    Package = new Package
                    {
                        PackageRegistrationKey = registrationKey
                    },

                    // The package that is depended on
                    Id = dependsOn
                };
            }
        }

        public class TheFindAllPackagesMethod : FactsBase
        {
            [Fact]
            public void FindsAllPackages()
            {
                // Arrange
                _context.Mock(packageRegistrations: new[]
                {
                    new PackageRegistration { Key = 1 },
                    new PackageRegistration { Key = 3 },
                });

                var expected = new HashSet<int> { 1, 3 };

                // Act & assert
                var actual = _target.FindAllPackages(new HashSet<int>());

                Assert.Equal(expected, actual);
            }

            [Fact]
            public void ExcludesIdsFromExceptParameter()
            {
                // Arrange
                _context.Mock(packageRegistrations: new[]
                {
                    new PackageRegistration { Key = 1 },
                    new PackageRegistration { Key = 3 },
                });

                var expected = new HashSet<int> { 3 };

                // Act & assert
                var actual = _target.FindAllPackages(new HashSet<int>{ 1 });

                Assert.Equal(expected, actual);
            }
        }

        public class TheFindPackageRegistrationInformationMethod : FactsBase
        {
            [Fact]
            public async Task FindsRegistrationInformation()
            {
                // Arrange
                // Package registration 3 isn't in the input set and should be ignored.
                var package1 = new Package { PackageRegistrationKey = 1 };
                var package2 = new Package { PackageRegistrationKey = 2 };
                var package3 = new Package { PackageRegistrationKey = 3 };
                var package4 = new Package { PackageRegistrationKey = 1 };

                _context.Mock(
                    packageRegistrations: new[]
                    {
                        new PackageRegistration {
                            Key = 1,
                            Id = "1",
                            DownloadCount = 10,
                            Packages = new[] { package1, package4 }
                        },
                        new PackageRegistration
                        {
                            Key = 2,
                            Id = "2",
                            DownloadCount = 1,
                            Packages = new[] { package2 }
                        },
                        new PackageRegistration
                        {
                            Key = 3,
                            Id = "3",
                            DownloadCount = 9,
                            Packages = new[] { package3 }
                        },
                    },
                    packages: new[]
                    {
                        package1,
                        package2,
                        package3,
                        package4
                    });

                // Act and assert
                var actual = await _target.FindPackageRegistrationInformationAsync("Name", new HashSet<int> { 1, 2 });

                Assert.Equal(2, actual.Count);
                Assert.Equal(1, actual[0].Key);
                Assert.Equal("1", actual[0].Id);
                Assert.Equal(2, actual[0].Versions);
                Assert.Equal(10, actual[0].Downloads);

                Assert.Equal(2, actual[1].Key);
                Assert.Equal("2", actual[1].Id);
                Assert.Equal(1, actual[1].Versions);
                Assert.Equal(1, actual[1].Downloads);
            }

            [Fact]
            public async Task PackageCountDoesntFilterPackagesThatWontBeRevalidated()
            {
                // Arrange
                // Package registration 3 isn't in the input set and should be ignored.
                _config.MaxPackageCreationDate = DateTimeOffset.UtcNow;

                var afterCutoff = _config.MaxPackageCreationDate.AddDays(1).DateTime;

                var package1 = new Package { PackageRegistrationKey = 1, PackageStatusKey = PackageStatus.FailedValidation };
                var package2 = new Package { PackageRegistrationKey = 1, PackageStatusKey = PackageStatus.Validating };
                var package3 = new Package { PackageRegistrationKey = 1, Created = afterCutoff };

                _context.Mock(
                    packageRegistrations: new[]
                    {
                        new PackageRegistration {
                            Key = 1,
                            Id = "1",
                            DownloadCount = 10,
                            Packages = new[] { package1, package2, package3 }
                        },
                    },
                    packages: new[]
                    {
                        package1,
                        package2,
                        package3,
                    });

                // Act & Assert
                var actual = await _target.FindPackageRegistrationInformationAsync("Name", new HashSet<int> { 1 });

                Assert.Single(actual);
                Assert.Equal(1, actual[0].Key);
                Assert.Equal("1", actual[0].Id);
                Assert.Equal(3, actual[0].Versions);
                Assert.Equal(10, actual[0].Downloads);
            }
        }

        public class TheFindAppropriateVersionsMethod : FactsBase
        {
            [Fact]
            public void FindsVersions()
            {
                // Arrange
                _config.MaxPackageCreationDate = DateTime.UtcNow;

                _context.Mock(packages: new[]
                {
                    new Package { PackageRegistrationKey = 1, NormalizedVersion = "1.0.0", PackageStatusKey = PackageStatus.Available },
                    new Package { PackageRegistrationKey = 2, NormalizedVersion = "3.0.0", PackageStatusKey = PackageStatus.Available },
                    new Package { PackageRegistrationKey = 1, NormalizedVersion = "2.0.0", PackageStatusKey = PackageStatus.Available },
                    new Package { PackageRegistrationKey = 3, NormalizedVersion = "2.0.0", PackageStatusKey = PackageStatus.Available },
                });

                var input = new List<PackageRegistrationInformation>
                {
                    new PackageRegistrationInformation { Key = 1 },
                    new PackageRegistrationInformation { Key = 2 },
                };

                // Act & Assert
                var actual = _target.FindAppropriateVersions(input);

                Assert.True(actual.ContainsKey(1));
                Assert.Equal(2, actual[1].Count);
                Assert.Equal(NuGetVersion.Parse("1.0.0"), actual[1][0]);
                Assert.Equal(NuGetVersion.Parse("2.0.0"), actual[1][1]);

                Assert.True(actual.ContainsKey(2));
                Assert.Single(actual[2]);
                Assert.Equal(NuGetVersion.Parse("3.0.0"), actual[2][0]);
            }

            [Fact]
            public void SkipsPackagesCreatedAfterMaxCreationDate()
            {
                // Arrange
                _config.MaxPackageCreationDate = DateTime.UtcNow;

                var beforeCutoff = _config.MaxPackageCreationDate.AddDays(-1).DateTime;
                var afterCutoff = _config.MaxPackageCreationDate.AddDays(1).DateTime;

                _context.Mock(packages: new[]
                {
                    new Package { PackageRegistrationKey = 1, NormalizedVersion = "1.0.0", PackageStatusKey = PackageStatus.Available, Created = beforeCutoff },
                    new Package { PackageRegistrationKey = 1, NormalizedVersion = "2.0.0", PackageStatusKey = PackageStatus.Available, Created = afterCutoff },
                });

                var input = new List<PackageRegistrationInformation>
                {
                    new PackageRegistrationInformation { Key = 1 }
                };

                // Act & Assert
                var actual = _target.FindAppropriateVersions(input);

                Assert.True(actual.ContainsKey(1));
                Assert.Single(actual[1]);
                Assert.Equal(NuGetVersion.Parse("1.0.0"), actual[1][0]);
            }

            [Fact]
            public void SkipsPackagesThatAreNotAvailableOrDeleted()
            {
                // Arrange
                _config.MaxPackageCreationDate = DateTime.UtcNow;

                _context.Mock(packages: new[]
                {
                    new Package { PackageRegistrationKey = 1, NormalizedVersion = "1.0.0", PackageStatusKey = PackageStatus.Available },
                    new Package { PackageRegistrationKey = 1, NormalizedVersion = "2.0.0", PackageStatusKey = PackageStatus.FailedValidation },
                    new Package { PackageRegistrationKey = 1, NormalizedVersion = "3.0.0", PackageStatusKey = PackageStatus.Validating },
                });

                var input = new List<PackageRegistrationInformation>
                {
                    new PackageRegistrationInformation { Key = 1 }
                };

                // Act & Assert
                var actual = _target.FindAppropriateVersions(input);

                Assert.True(actual.ContainsKey(1));
                Assert.Single(actual[1]);
                Assert.Equal(NuGetVersion.Parse("1.0.0"), actual[1][0]);
            }
        }

        public class TheAppropriatePackageCountMethod : FactsBase
        {
            [Fact]
            public void CountsPackages()
            {
                // Arrange
                _config.MaxPackageCreationDate = DateTimeOffset.UtcNow;

                var creationTime = _config.MaxPackageCreationDate.AddDays(-1).DateTime;

                _context.Mock(packages: new[]
                {
                    new Package { PackageStatusKey = PackageStatus.Available, Created = creationTime },
                    new Package { PackageStatusKey = PackageStatus.Available, Created = creationTime },
                    new Package { PackageStatusKey = PackageStatus.Available, Created = creationTime },
                });

                // Act and assert
                var actual = _target.AppropriatePackageCount();

                Assert.Equal(3, actual);
            }

            [Fact]
            public void SkipsPackagesAfterMaxCreationDate()
            {
                // Arrange
                _config.MaxPackageCreationDate = DateTimeOffset.UtcNow;

                var beforeCutoff = _config.MaxPackageCreationDate.AddDays(-1).DateTime;
                var afterCutoff = _config.MaxPackageCreationDate.AddDays(1).DateTime;

                _context.Mock(packages: new[]
                {
                    new Package { PackageStatusKey = PackageStatus.Available, Created = beforeCutoff },
                    new Package { PackageStatusKey = PackageStatus.Available, Created = afterCutoff },
                    new Package { PackageStatusKey = PackageStatus.Available, Created = beforeCutoff },
                });

                // Act and assert
                var actual = _target.AppropriatePackageCount();

                Assert.Equal(2, actual);
            }

            [Fact]
            public void SkipsPackagesThatAreNotAvailableOrDeleted()
            {
                // Arrange
                _config.MaxPackageCreationDate = DateTimeOffset.UtcNow;

                var creationTime = _config.MaxPackageCreationDate.AddDays(-1).DateTime;

                _context.Mock(packages: new[]
                {
                    new Package { PackageStatusKey = PackageStatus.Available, Created = creationTime },
                    new Package { PackageStatusKey = PackageStatus.Validating, Created = creationTime },
                    new Package { PackageStatusKey = PackageStatus.FailedValidation, Created = creationTime },
                });

                // Act and assert
                var actual = _target.AppropriatePackageCount();

                Assert.Equal(1, actual);
            }
        }

        public class FactsBase
        {
            public readonly Mock<IEntitiesContext> _context;
            public readonly Mock<IServiceScopeFactory> _scopeFactory;

            public readonly InitializationConfiguration _config;
            public readonly PackageFinder _target;

            public FactsBase()
            {
                _context = new Mock<IEntitiesContext>();
                _scopeFactory = new Mock<IServiceScopeFactory>();
                _config = new InitializationConfiguration();

                var scope = new Mock<IServiceScope>();
                var serviceProvider = new Mock<IServiceProvider>();

                _scopeFactory.Setup(s => s.CreateScope()).Returns(scope.Object);
                scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);
                serviceProvider.Setup(p => p.GetService(typeof(IEntitiesContext))).Returns(_context.Object);

                _target = new PackageFinder(
                    _context.Object,
                    _scopeFactory.Object,
                    _config,
                    Mock.Of<ILogger<PackageFinder>>());
            }
        }
    }
}

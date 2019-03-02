// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageDeprecationServiceFacts
    {
        public class TheUpdateDeprecationMethod : TestContainer
        {
            public static IEnumerable<object[]> ThrowsIfPackagesEmpty_Data => MemberDataHelper.AsDataSet(null, new Package[0]);

            [Theory]
            [MemberData(nameof(ThrowsIfPackagesEmpty_Data))]
            public async Task ThrowsIfPackagesEmpty(IReadOnlyCollection<Package> packages)
            {
                var service = Get<PackageDeprecationService>();

                await Assert.ThrowsAsync<ArgumentException>(() =>
                    service.UpdateDeprecation(
                        packages,
                        PackageDeprecationStatus.NotDeprecated,
                        new Cve[0],
                        null,
                        new Cwe[0],
                        null,
                        null,
                        null,
                        false));
            }

            [Fact]
            public async Task ThrowsIfCvesNull()
            {
                var package = new Package();

                var service = Get<PackageDeprecationService>();

                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                    service.UpdateDeprecation(
                        new[] { package },
                        PackageDeprecationStatus.NotDeprecated,
                        null,
                        null,
                        new Cwe[0],
                        null,
                        null,
                        null,
                        false));
            }

            [Fact]
            public async Task ThrowsIfCwesNull()
            {
                var package = new Package();

                var service = Get<PackageDeprecationService>();

                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                    service.UpdateDeprecation(
                        new[] { package },
                        PackageDeprecationStatus.NotDeprecated,
                        new Cve[0],
                        null,
                        null,
                        null,
                        null,
                        null,
                        false));
            }

            [Fact]
            public async Task DeletesExistingDeprecationsIfStatusNotDeprecated()
            {
                // Arrange
                var packageWithDeprecation1 = new Package
                {
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() }
                };

                var packageWithoutDeprecation1 = new Package();

                var packageWithDeprecation2 = new Package
                {
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() }
                };

                var packageWithoutDeprecation2 = new Package();

                var packages = new[] 
                {
                    packageWithDeprecation1,
                    packageWithoutDeprecation1,
                    packageWithDeprecation2,
                    packageWithoutDeprecation2
                };

                var deprecationRepository = GetMock<IEntityRepository<PackageDeprecation>>();
                var existingDeprecations = packages.Select(p => p.Deprecations.SingleOrDefault()).Where(d => d != null).ToList();
                deprecationRepository
                    .Setup(x => x.DeleteOnCommit(It.Is<IEnumerable<PackageDeprecation>>(d => d.SequenceEqual(existingDeprecations))))
                    .Verifiable();

                deprecationRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Completes()
                    .Verifiable();

                var service = Get<PackageDeprecationService>();

                // Act
                await service.UpdateDeprecation(
                    packages,
                    PackageDeprecationStatus.NotDeprecated,
                    new Cve[0],
                    null,
                    new Cwe[0],
                    null,
                    null,
                    null,
                    false);

                // Assert
                deprecationRepository.Verify();

                foreach (var package in packages)
                {
                    Assert.Empty(package.Deprecations);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReplacesExistingDeprecations(bool shouldUnlist)
            {
                // Arrange
                var unlistedPackageWithoutDeprecation = new Package();

                var packageWithDeprecation1 = new Package
                {
                    Listed = true,
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() }
                };

                var packageWithoutDeprecation1 = new Package
                {
                    Listed = true,
                };

                var packageWithDeprecation2 = new Package
                {
                    Listed = true,
                    Deprecations = new List<PackageDeprecation>
                    {
                        new PackageDeprecation
                        {
                            Cves = new List<Cve>
                            {
                                new Cve
                                {
                                    CveId = "cve-0"
                                }
                            }
                        }
                    }
                };

                var packageWithoutDeprecation2 = new Package
                {
                    Listed = true,
                };

                var packageWithDeprecation3 = new Package
                {
                    Listed = true,
                    Deprecations = new List<PackageDeprecation>
                    {
                        new PackageDeprecation
                        {
                            Cwes = new List<Cwe>
                            {
                                new Cwe
                                {
                                    CweId = "cwe-0"
                                }
                            }
                        }
                    }
                };

                var packages = new[]
                {
                    unlistedPackageWithoutDeprecation,
                    packageWithDeprecation1,
                    packageWithoutDeprecation1,
                    packageWithDeprecation2,
                    packageWithoutDeprecation2,
                    packageWithDeprecation3
                };

                var deprecationRepository = GetMock<IEntityRepository<PackageDeprecation>>();
                var packagesWithoutDeprecations = packages.Where(p => p.Deprecations.SingleOrDefault() == null).ToList();
                deprecationRepository
                    .Setup(x => x.InsertOnCommit(
                        It.Is<IEnumerable<PackageDeprecation>>(
                            // The deprecations inserted must be identical to the list of packages without deprecations.
                            i => packagesWithoutDeprecations.SequenceEqual(i.Select(d => d.Package)))))
                    .Verifiable();

                deprecationRepository
                    .Setup(x => x.CommitChangesAsync())
                    .Completes()
                    .Verifiable();

                var service = Get<PackageDeprecationService>();

                var status = (PackageDeprecationStatus)99;

                var cves = new []
                {
                    new Cve
                    {
                        CveId = "cve-1"
                    },

                    new Cve
                    {
                        CveId = "cve-2"
                    }
                };

                var cvss = (decimal)5.5;

                var cwes = new[]
                {
                    new Cwe
                    {
                        CweId = "cwe-1"
                    },

                    new Cwe
                    {
                        CweId = "cwe-2"
                    }
                };

                var alternatePackageRegistration = new PackageRegistration();
                var alternatePackage = new Package();

                var customMessage = "message";

                // Act
                await service.UpdateDeprecation(
                    packages,
                    status,
                    cves,
                    cvss,
                    cwes,
                    alternatePackageRegistration,
                    alternatePackage,
                    customMessage,
                    shouldUnlist);

                // Assert
                deprecationRepository.Verify();

                foreach (var package in packages)
                {
                    var deprecation = package.Deprecations.Single();
                    Assert.Equal(status, deprecation.Status);
                    Assert.Equal(cves, deprecation.Cves);
                    Assert.Equal(cvss, deprecation.CvssRating);
                    Assert.Equal(cwes, deprecation.Cwes);
                    Assert.Equal(alternatePackageRegistration, deprecation.AlternatePackageRegistration);
                    Assert.Equal(alternatePackage, deprecation.AlternatePackage);
                    Assert.Equal(customMessage, deprecation.CustomMessage);
                    
                    if (shouldUnlist)
                    {
                        Assert.False(package.Listed);
                    }
                    else if (package != unlistedPackageWithoutDeprecation)
                    {
                        Assert.True(package.Listed);
                    }
                }
            }
        }

        public class TheGetCvesByIdMethod : TestContainer
        {
            public static IEnumerable<object[]> CommitChanges_Data => 
                MemberDataHelper.BooleanDataSet();

            [Theory]
            [MemberData(nameof(CommitChanges_Data))]
            public Task ThrowsIfNullIdList(bool commitChanges)
            {
                var service = Get<PackageDeprecationService>();

                return Assert.ThrowsAsync<ArgumentNullException>(() => service.GetOrCreateCvesByIdAsync(null, commitChanges));
            }

            [Theory]
            [MemberData(nameof(CommitChanges_Data))]
            public async Task ReturnsExistingAndCreatedCves(bool commitChanges)
            {
                // Arrange
                var matchingCve1 = new Cve
                {
                    CveId = "cve-1"
                };

                var notMatchingCve1 = new Cve
                {
                    CveId = "cve-2"
                };

                var matchingCve2 = new Cve
                {
                    CveId = "cve-3"
                };

                var notMatchingCve2 = new Cve
                {
                    CveId = "cve-4"
                };

                var cves = new[] { matchingCve1, notMatchingCve1, matchingCve2, notMatchingCve2 };
                var repository = GetMock<IEntityRepository<Cve>>();
                repository
                    .Setup(x => x.GetAll())
                    .Returns(cves.AsQueryable())
                    .Verifiable();

                var service = Get<PackageDeprecationService>();

                var missingCveId = "cve-5";
                var queriedCveIds = new[] { matchingCve1.CveId, matchingCve2.CveId, missingCveId };

                // Act
                var result = await service.GetOrCreateCvesByIdAsync(queriedCveIds, commitChanges);

                // Assert
                Assert.Equal(3, result.Count);
                Assert.Contains(matchingCve1, result);
                Assert.Contains(matchingCve2, result);

                var createdCve = result.Last();
                Assert.Equal(missingCveId, createdCve.CveId);
                Assert.False(createdCve.Listed);
                Assert.Equal(CveStatus.Unknown, createdCve.Status);
                Assert.Null(createdCve.Description);
                Assert.Null(createdCve.CvssRating);
                Assert.Null(createdCve.LastModifiedDate);
                Assert.Null(createdCve.PublishedDate);
            }
        }

        public class TheGetCwesByIdMethod : TestContainer
        {
            [Fact]
            public void ThrowsIfNullIdList()
            {
                var service = Get<PackageDeprecationService>();

                Assert.Throws<ArgumentNullException>(() => service.GetCwesById(null));
            }

            [Fact]
            public void ReturnsExistingAndCreatedCwes(bool commitChanges)
            {
                // Arrange
                var matchingCwe1 = new Cwe
                {
                    CweId = "cve-1"
                };

                var notMatchingCwe1 = new Cwe
                {
                    CweId = "cve-2"
                };

                var matchingCwe2 = new Cwe
                {
                    CweId = "cve-3"
                };

                var notMatchingCwe2 = new Cwe
                {
                    CweId = "cve-4"
                };

                var cwes = new[] { matchingCwe1, notMatchingCwe1, matchingCwe2, notMatchingCwe2 };
                var repository = GetMock<IEntityRepository<Cwe>>();
                repository
                    .Setup(x => x.GetAll())
                    .Returns(cwes.AsQueryable())
                    .Verifiable();

                var service = Get<PackageDeprecationService>();

                var queriedCweIds = new[] { matchingCwe1.CweId, matchingCwe2.CweId };

                // Act
                var result = service.GetCwesById(queriedCweIds);

                // Assert
                Assert.Equal(2, result.Count);
                Assert.Contains(matchingCwe1, result);
                Assert.Contains(matchingCwe2, result);
            }

            [Fact]
            public void ReturnsExistingAndCreatedCwes()
            {
                // Arrange
                var matchingCwe1 = new Cwe
                {
                    CweId = "cve-1"
                };

                var notMatchingCwe1 = new Cwe
                {
                    CweId = "cve-2"
                };

                var matchingCwe2 = new Cwe
                {
                    CweId = "cve-3"
                };

                var notMatchingCwe2 = new Cwe
                {
                    CweId = "cve-4"
                };

                var cwes = new[] { matchingCwe1, notMatchingCwe1, matchingCwe2, notMatchingCwe2 };
                var repository = GetMock<IEntityRepository<Cwe>>();
                repository
                    .Setup(x => x.GetAll())
                    .Returns(cwes.AsQueryable())
                    .Verifiable();

                var service = Get<PackageDeprecationService>();

                var missingCweId = "cwe-5";
                var queriedCweIds = new[] { matchingCwe1.CweId, matchingCwe2.CweId, missingCweId };

                // Act
                Assert.Throws<ArgumentException>(() => service.GetCwesById(queriedCweIds));
            }
        }
    }
}

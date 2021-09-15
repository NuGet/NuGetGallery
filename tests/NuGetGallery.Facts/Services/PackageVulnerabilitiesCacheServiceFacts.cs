// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageVulnerabilitiesCacheServiceFacts
    {
        private Mock<IServiceScope> _serviceScopeMock;
        private Mock<ITelemetryService> _telemetryServiceMock;
        private PackageVulnerabilitiesCacheService _cacheService;

        [Fact]
        public void RefreshesVulnerabilitiesCache()
        {
            // Arrange
            Setup();

            // Act
            var vulnerabilitiesFoo = _cacheService.GetVulnerabilitiesById("Foo");
            var vulnerabilitiesBar = _cacheService.GetVulnerabilitiesById("Bar");

            // Assert
            // - ensure telemetry is sent
            _telemetryServiceMock.Verify(x => x.TrackVulnerabilitiesCacheRefreshDuration(It.IsAny<TimeSpan>()), Times.Once);
            // - ensure scope is disposed
            _serviceScopeMock.Verify(x => x.Dispose(), Times.AtLeastOnce);
            // - ensure contants of cache are correct
            Assert.Equal(4, vulnerabilitiesFoo.Count);
            Assert.Equal(1, vulnerabilitiesFoo[0].Count);
            Assert.Equal(1, vulnerabilitiesFoo[1].Count);
            Assert.Equal(2, vulnerabilitiesFoo[2].Count);
            Assert.Equal(1234, vulnerabilitiesFoo[2][0].GitHubDatabaseKey);
            Assert.Equal(5678, vulnerabilitiesFoo[2][1].GitHubDatabaseKey);
            Assert.Equal(1, vulnerabilitiesFoo[3].Count);
            Assert.Equal(2, vulnerabilitiesBar.Count);
            Assert.Equal(1, vulnerabilitiesBar[5].Count);
            Assert.Equal(9012, vulnerabilitiesBar[5][0].GitHubDatabaseKey);
            Assert.Equal(1, vulnerabilitiesBar[6].Count);
        }

        [Theory]
        [InlineData("Foo", 4)]
        [InlineData("Bar", 2)]
        [InlineData("FOo", 4)]
        [InlineData("FoO", 4)]
        [InlineData("Bär", 2)]
        [InlineData("BÄr", 2)]
        [InlineData("ÇCombinedWithCedilla", 1)] // first char here is C combined with cedilla diacritic: /u00C7 ** actual match
        [InlineData("çCombinedWithCedilla", 1)] // first char here is c combined with cedilla diacritic: /u00E7
        [InlineData("çCombinedWithCedilla", 1)] // first char here c followed by combining cedilla diacritic: /u0063 /u0327
        [InlineData("cCombinedWithCedilla", 0)] // first char here c only (not a match with a c-cedilla): /u0063
        [InlineData("ÇFollowedByCedilla", 1)] // first char here is C combined with cedilla diacritic: /u00C7
        [InlineData("çFollowedByCedilla", 1)] // first char here is c combined with cedilla diacritic: /u00E7
        [InlineData("çFollowedByCedilla", 1)] // first char here c followed by combining cedilla diacritic: /u0063 /u0327 ** actual match
        [InlineData("cFollowedByCedilla", 0)] // first char here c only (not a match with a c-cedilla): /u0063
        public void CacheSupportsCaseInsensitiveLookups(string packageLookupId, int expectedVulnerabilitiesCount)
        {
            // Arrange
            Setup();

            // Act
            var vulnerabilitiesFoo = _cacheService.GetVulnerabilitiesById(packageLookupId);

            // Assert
            Assert.Equal(expectedVulnerabilitiesCount, vulnerabilitiesFoo?.Count ?? 0);
        }

        private void Setup()
        {
            var entitiesContext = new Mock<IEntitiesContext>();
            entitiesContext.Setup(x => x.Set<VulnerablePackageVersionRange>()).Returns(GetVulnerableRanges());
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(x => x.GetService(typeof(IEntitiesContext))).Returns(entitiesContext.Object);
            _serviceScopeMock = new Mock<IServiceScope>();
            _serviceScopeMock.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
            _serviceScopeMock.Setup(x => x.Dispose()).Verifiable();
            var serviceScopeFactory = new Mock<IServiceScopeFactory>();
            serviceScopeFactory.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
            _telemetryServiceMock = new Mock<ITelemetryService>();
            _telemetryServiceMock.Setup(x => x.TrackVulnerabilitiesCacheRefreshDuration(It.IsAny<TimeSpan>())).Verifiable();
            _cacheService = new PackageVulnerabilitiesCacheService(_telemetryServiceMock.Object);
            _cacheService.RefreshCache(serviceScopeFactory.Object);
        }

        private static DbSet<VulnerablePackageVersionRange> GetVulnerableRanges()
        {
            var registrationFoo = new PackageRegistration { Id = "Foo" };
            var registrationBar = new PackageRegistration { Id = "Bar" };
            var registrationBarNonLatin = new PackageRegistration { Id = "Bär" };
            var registrationCedillaNonLatin = new PackageRegistration { Id = "ÇCombinedWithCedilla" }; // Ç is /u00C7
            var registrationCedillaNonLatin2 = new PackageRegistration { Id = "çFollowedByCedilla" }; // ç is /u0063 /u0327

            var vulnerabilityCriticalFoo = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/1234",
                GitHubDatabaseKey = 1234,
                Severity = PackageVulnerabilitySeverity.Critical
            };
            var vulnerabilityModerateFoo = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/5678",
                GitHubDatabaseKey = 5678,
                Severity = PackageVulnerabilitySeverity.Moderate
            };
            var vulnerabilityCriticalBar = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/9012",
                GitHubDatabaseKey = 9012,
                Severity = PackageVulnerabilitySeverity.Critical
            };
            var vulnerabilityCriticalBarNonLatin = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/2109",
                GitHubDatabaseKey = 2109,
                Severity = PackageVulnerabilitySeverity.Critical
            };
            var vulnerabilityCriticalCedillaNonLatin = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/2901",
                GitHubDatabaseKey = 2901,
                Severity = PackageVulnerabilitySeverity.Critical
            };
            var vulnerabilityCriticalCedillaNonLatin2 = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/29012",
                GitHubDatabaseKey = 29012,
                Severity = PackageVulnerabilitySeverity.Critical
            };

            var versionRangeCriticalFoo = new VulnerablePackageVersionRange
            {
                Vulnerability = vulnerabilityCriticalFoo,
                PackageId = "Foo",
                PackageVersionRange = "1.1.1",
                FirstPatchedPackageVersion = "1.1.2"
            };
            var versionRangeModerateFoo = new VulnerablePackageVersionRange
            {
                Vulnerability = vulnerabilityModerateFoo,
                PackageId = "Foo",
                PackageVersionRange = "<=1.1.2",
                FirstPatchedPackageVersion = "1.1.3"
            };
            var versionRangeCriticalBar = new VulnerablePackageVersionRange
            {
                Vulnerability = vulnerabilityCriticalBar,
                PackageId = "Bar",
                PackageVersionRange = "<=1.1.0",
                FirstPatchedPackageVersion = "1.1.1"
            };
            var versionRangeCriticalBarNonLatin = new VulnerablePackageVersionRange
            {
                Vulnerability = vulnerabilityCriticalBarNonLatin,
                PackageId = "Bär",
                PackageVersionRange = "<=1.1.0",
                FirstPatchedPackageVersion = "1.1.1"
            };
            var versionRangeCriticalCedillaNonLatin = new VulnerablePackageVersionRange
            {
                Vulnerability = vulnerabilityCriticalCedillaNonLatin,
                PackageId = "ÇCombinedWithCedilla",
                PackageVersionRange = "<=1.0.0",
                FirstPatchedPackageVersion = "1.1.1"
            };
            var versionRangeCriticalCedillaNonLatin2 = new VulnerablePackageVersionRange
            {
                Vulnerability = vulnerabilityCriticalCedillaNonLatin2,
                PackageId = "çFollowedByCedilla",
                PackageVersionRange = "<=1.0.0",
                FirstPatchedPackageVersion = "1.1.1"
            };

            var packageFoo100 = new Package
            {
                Key = 0,
                PackageRegistration = registrationFoo,
                Version = "1.0.0",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeModerateFoo
                }
            };
            var packageFoo110 = new Package
            {
                Key = 1,
                PackageRegistration = registrationFoo,
                Version = "1.1.0",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeModerateFoo
                }
            };
            var packageFoo111 = new Package
            {
                Key = 2,
                PackageRegistration = registrationFoo,
                Version = "1.1.1",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeModerateFoo,
                    versionRangeCriticalFoo
                }
            };
            var packageFoo112 = new Package
            {
                Key = 3,
                PackageRegistration = registrationFoo,
                Version = "1.1.2",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeModerateFoo
                }
            };
            var packageBar100 = new Package
            {
                Key = 5,
                Version = "1.0.0",
                PackageRegistration = registrationBar,
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeCriticalBar
                }
            };
            var packageBar110 = new Package
            {
                Key = 6,
                PackageRegistration = registrationBar,
                Version = "1.1.0",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeCriticalBar
                }
            };
            var packageBarNonLatin100 = new Package
            {
                Key = 5,
                Version = "1.0.0",
                PackageRegistration = registrationBarNonLatin,
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeCriticalBarNonLatin
                }
            };
            var packageBarNonLatin110 = new Package
            {
                Key = 6,
                PackageRegistration = registrationBarNonLatin,
                Version = "1.1.0",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeCriticalBarNonLatin
                }
            };
            var packageCedillaNonLatin100 = new Package
            {
                Key = 5,
                Version = "1.0.0",
                PackageRegistration = registrationCedillaNonLatin,
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeCriticalCedillaNonLatin
                }
            };
            var packageCedillaNonLatin2_100 = new Package
            {
                Key = 5,
                Version = "1.0.0",
                PackageRegistration = registrationCedillaNonLatin2,
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    versionRangeCriticalCedillaNonLatin2
                }
            };

            versionRangeCriticalFoo.Packages = new List<Package> { packageFoo111 };
            versionRangeModerateFoo.Packages = new List<Package> { packageFoo100, packageFoo110, packageFoo111, packageFoo112 };
            versionRangeCriticalBar.Packages = new List<Package> { packageBar100, packageBar110 };
            versionRangeCriticalBarNonLatin.Packages = new List<Package> { packageBarNonLatin100, packageBarNonLatin110 };
            versionRangeCriticalCedillaNonLatin.Packages = new List<Package> { packageCedillaNonLatin100 };
            versionRangeCriticalCedillaNonLatin2.Packages = new List<Package> { packageCedillaNonLatin2_100 };

            var vulnerableRangeList = new List<VulnerablePackageVersionRange> { versionRangeCriticalFoo, versionRangeModerateFoo, versionRangeCriticalBar,
                    versionRangeCriticalBarNonLatin, versionRangeCriticalCedillaNonLatin, versionRangeCriticalCedillaNonLatin2 }
                .AsQueryable();
            var vulnerableRangeDbSet = new Mock<DbSet<VulnerablePackageVersionRange>>();

            // boilerplate mock DbSet redirects:
            vulnerableRangeDbSet.As<IQueryable>().Setup(x => x.Provider).Returns(vulnerableRangeList.Provider);
            vulnerableRangeDbSet.As<IQueryable>().Setup(x => x.Expression).Returns(vulnerableRangeList.Expression);
            vulnerableRangeDbSet.As<IQueryable>().Setup(x => x.ElementType).Returns(vulnerableRangeList.ElementType);
            vulnerableRangeDbSet.As<IQueryable>().Setup(x => x.GetEnumerator()).Returns(vulnerableRangeList.GetEnumerator());
            vulnerableRangeDbSet.Setup(x => x.Include(It.IsAny<string>())).Returns(vulnerableRangeDbSet.Object); // bypass includes (which break the test)

            return vulnerableRangeDbSet.Object;
        }
    }
}

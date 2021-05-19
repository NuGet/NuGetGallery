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
    public class PackageVulnerabilitiesCacheServiceFacts : TestContainer
    {
        [Fact]
        public void RefreshesVulnerabilitiesCache()
        {
            // Arrange
            var entitiesContext = new Mock<IEntitiesContext>();
            entitiesContext.Setup(x => x.Set<VulnerablePackageVersionRange>()).Returns(GetVulnerableRanges());
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(x => x.GetService(typeof(IEntitiesContext))).Returns(entitiesContext.Object);
            var serviceScope = new Mock<IServiceScope>();
            serviceScope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
            serviceScope.Setup(x => x.Dispose()).Verifiable();
            var serviceScopeFactory = new Mock<IServiceScopeFactory>();
            serviceScopeFactory.Setup(x => x.CreateScope()).Returns(serviceScope.Object);
            var telemetryService = new Mock<ITelemetryService>();
            telemetryService.Setup(x => x.TrackVulnerabilitiesCacheRefreshDurationMs(It.IsAny<long>())).Verifiable();
            var cacheService = new PackageVulnerabilitiesCacheService(telemetryService.Object);
            cacheService.RefreshCache(serviceScopeFactory.Object);

            // Act
            var vulnerabilitiesFoo = cacheService.GetVulnerabilitiesById("Foo");
            var vulnerabilitiesBar = cacheService.GetVulnerabilitiesById("Bar");

            // Assert
            // - ensure telemetry is sent
            telemetryService.Verify(x => x.TrackVulnerabilitiesCacheRefreshDurationMs(It.IsAny<long>()), Times.Once);
            // - ensure scope is disposed
            serviceScope.Verify(x => x.Dispose(), Times.AtLeastOnce);
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

        DbSet<VulnerablePackageVersionRange> GetVulnerableRanges()
        {
            var registrationFoo = new PackageRegistration { Id = "Foo" };
            var registrationBar = new PackageRegistration { Id = "Bar" };

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

            versionRangeCriticalFoo.Packages = new List<Package> { packageFoo111 };
            versionRangeModerateFoo.Packages = new List<Package> { packageFoo100, packageFoo110, packageFoo111, packageFoo112 };
            versionRangeCriticalBar.Packages = new List<Package> { packageBar100, packageBar110 };

            var vulnerableRangeList = new List<VulnerablePackageVersionRange> { versionRangeCriticalFoo, versionRangeModerateFoo, versionRangeCriticalBar }.AsQueryable();
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageVulnerabilitiesCacheServiceFacts : TestContainer
    {
        [Fact]
        public void InitializesVulnerabilitiesCache()
        {
            // Arrange
            var vulnerableVersionRanges = GetVersionRanges();
            var pvmService = new Mock<IPackageVulnerabilitiesManagementService>();
            pvmService.Setup(stub => stub.GetAllVulnerableRanges()).Returns(vulnerableVersionRanges);
            pvmService.Setup(stub => stub.GetVulnerableRangesById(It.IsAny<string>())).Verifiable();
            var cacheService = new PackageVulnerabilitiesCacheService(pvmService.Object);

            // Act
            var vulnerabilitiesFoo = cacheService.GetVulnerabilitiesById("Foo");
            var vulnerabilitiesBar = cacheService.GetVulnerabilitiesById("Bar");

            // Assert
            // This method should never be called (it's only called when cache can't provide, and these values are loaded into the cache on initialize)
            pvmService.Verify(s => s.GetVulnerableRangesById(It.IsAny<string>()), Times.Never);
            // Test cache contents
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

        private IQueryable<VulnerablePackageVersionRange> GetVersionRanges()
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

            versionRangeCriticalFoo.Packages = new List<Package> {packageFoo111};
            versionRangeModerateFoo.Packages = new List<Package> {packageFoo100, packageFoo110, packageFoo111, packageFoo112};
            versionRangeCriticalBar.Packages = new List<Package> {packageBar100, packageBar110};

            return new List<VulnerablePackageVersionRange>
                {versionRangeCriticalFoo, versionRangeModerateFoo, versionRangeCriticalBar}.AsQueryable();
        }
    }
}

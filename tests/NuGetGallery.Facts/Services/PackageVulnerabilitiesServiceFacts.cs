// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageVulnerabilitiesServiceFacts : TestContainer
    {
        private PackageRegistration _registrationVulnerable;

        private PackageVulnerability _vulnerabilityCritical;
        private PackageVulnerability _vulnerabilityModerate;

        private VulnerablePackageVersionRange _versionRangeCritical;
        private VulnerablePackageVersionRange _versionRangeModerate;

        private Package _packageVulnerable100;
        private Package _packageVulnerable110;
        private Package _packageVulnerable111;

        private Package _packageNotVulnerable;

        [Fact]
        public void GetsVulnerabilitiesOfPackage()
        {
            // Arrange
            SetUp();
            var vulnerableRanges = new[] {_versionRangeModerate, _versionRangeCritical};
            var context = GetFakeContext();
            context.VulnerableRanges.AddRange(vulnerableRanges);
            var target = Get<PackageVulnerabilitiesService>();

            // Act
            var vulnerableResult = target.GetVulnerabilitiesById("Vulnerable");
            var notVulnerableResult = target.GetVulnerabilitiesById("NotVulnerable");

            // Assert
            Assert.Equal(3, vulnerableResult.Count);
            var vulnerabilitiesFor100 = vulnerableResult[_packageVulnerable100.Key];
            var vulnerabilitiesFor110 = vulnerableResult[_packageVulnerable110.Key];
            var vulnerabilitiesFor111 = vulnerableResult[_packageVulnerable111.Key];
            Assert.Equal(_vulnerabilityModerate, vulnerabilitiesFor100[0]);
            Assert.Equal(_vulnerabilityModerate, vulnerabilitiesFor110[0]);
            Assert.Equal(_vulnerabilityModerate, vulnerabilitiesFor111[0]);
            Assert.Equal(_vulnerabilityCritical, vulnerabilitiesFor111[1]);

            Assert.Null(notVulnerableResult);
        }

        [Fact]
        public void GetsVulnerableStatusOfPackage()
        {
            // Arrange
            SetUp();
            var context = GetFakeContext();
            var target = Get<PackageVulnerabilitiesService>();

            // Act
            var shouldBeVulnerable = target.IsPackageVulnerable(_packageVulnerable100);
            var shouldNotBeVulnerable = target.IsPackageVulnerable(_packageNotVulnerable);

            // Assert
            Assert.True(shouldBeVulnerable);
            Assert.False(shouldNotBeVulnerable);
        }

        private void SetUp()
        {
            _registrationVulnerable = new PackageRegistration { Id = "Vulnerable" };

            _vulnerabilityCritical = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/1234",
                GitHubDatabaseKey = 1234,
                Severity = PackageVulnerabilitySeverity.Critical
            };
            _vulnerabilityModerate = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/5678",
                GitHubDatabaseKey = 5678,
                Severity = PackageVulnerabilitySeverity.Moderate
            };

            _versionRangeCritical = new VulnerablePackageVersionRange
            {
                PackageId = "Vulnerable",
                Vulnerability = _vulnerabilityCritical,
                PackageVersionRange = "1.1.1",
                FirstPatchedPackageVersion = "1.1.2"
            };
            _versionRangeModerate = new VulnerablePackageVersionRange
            {
                PackageId = "Vulnerable",
                Vulnerability = _vulnerabilityModerate,
                PackageVersionRange = "<=1.1.1",
                FirstPatchedPackageVersion = "1.1.2"
            };

            _packageVulnerable100 = new Package
            {
                Key = 0,
                PackageRegistration = _registrationVulnerable,
                Version = "1.0.0",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    _versionRangeModerate
                }
            };
            _packageVulnerable110 = new Package
            {
                Key = 1,
                PackageRegistration = _registrationVulnerable,
                Version = "1.1.0",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    _versionRangeModerate
                }
            };
            _packageVulnerable111 = new Package
            {
                Key = 2,
                PackageRegistration = _registrationVulnerable,
                Version = "1.1.1",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    _versionRangeModerate,
                    _versionRangeCritical
                }
            };
            _packageNotVulnerable = new Package
            {
                Key = 3,
                PackageRegistration = new PackageRegistration { Id = "NotVulnerable" },
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>()
            };

            _versionRangeCritical.Packages = new List<Package> { _packageVulnerable111 };
            _versionRangeModerate.Packages = new List<Package> { _packageVulnerable100, _packageVulnerable110, _packageVulnerable111 };
        }
    }
}

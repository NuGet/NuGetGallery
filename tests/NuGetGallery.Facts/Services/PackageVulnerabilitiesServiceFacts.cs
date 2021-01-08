﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        private PackageRegistration _registrationNotVulnerable;

        private PackageVulnerability _vulnerabilityCritical;
        private PackageVulnerability _vulnerabilityModerate;

        private VulnerablePackageVersionRange _versionRangeCritical;
        private VulnerablePackageVersionRange _versionRangeModerate;

        private Package _packageVulnerable100;
        private Package _packageVulnerable110;
        private Package _packageVulnerable111;
        private Package _packageVulnerable112;

        private Package _packageNotVulnerable010;
        private Package _packageNotVulnerable011;

        private Package[] _packages;

        [Fact]
        public void GetsVulnerabilitiesOfPackage()
        {
            // Arrange
            SetUp();
            var context = GetFakeContext();
            context.Packages.AddRange(_packages);
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
        public void GetsVulnerableStatusOfPackageGroup()
        {
            // Arrange
            SetUp();
            var context = GetFakeContext();
            context.Packages.AddRange(_packages);
            var target = Get<PackageVulnerabilitiesService>();

            // Act
            var shouldBeVulnerable =
                target.PackagesContainVulnerability(_packages.AsQueryable().Where(p => p.Id == "Vulnerable"));
            var shouldNotBeVulnerable =
                target.PackagesContainVulnerability(_packages.AsQueryable().Where(p => p.Id == "NotVulnerable"));

            // Assert
            Assert.True(shouldBeVulnerable);
            Assert.False(shouldNotBeVulnerable);
        }

        private void SetUp()
        {
            _registrationVulnerable = new PackageRegistration { Id = "Vulnerable" };
            _registrationNotVulnerable = new PackageRegistration { Id = "NotVulnerable" };

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
                Vulnerability = _vulnerabilityCritical,
                PackageVersionRange = "1.1.1",
                FirstPatchedPackageVersion = "1.1.2"
            };
            _versionRangeModerate = new VulnerablePackageVersionRange
            {
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
                Key = 3, // simulate a different order in db - create a non-contiguous range of rows, even if the range is contiguous
                PackageRegistration = _registrationVulnerable,
                Version = "1.1.1",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>
                {
                    _versionRangeModerate,
                    _versionRangeCritical
                }
            };
            _packageVulnerable112 = new Package
            {
                Key = 2, // simulate a different order in db  - create a non-contiguous range of rows, even if the range is contiguous
                PackageRegistration = _registrationVulnerable,
                Version = "1.1.2",
                VulnerablePackageRanges = null
            };
            _packageNotVulnerable010 = new Package
            {
                Key = 4,
                PackageRegistration = _registrationNotVulnerable,
                Version = "0.1.0",
                VulnerablePackageRanges = null
            };
            _packageNotVulnerable011 = new Package
            {
                Key = 5,
                PackageRegistration = _registrationNotVulnerable,
                Version = "0.1.1",
                VulnerablePackageRanges = null
            };

            _packages = new[]
            {
                _packageVulnerable100,
                _packageVulnerable110,
                _packageVulnerable111,
                _packageVulnerable112,
                _packageNotVulnerable010,
                _packageNotVulnerable011,
            };
        }
    }
}

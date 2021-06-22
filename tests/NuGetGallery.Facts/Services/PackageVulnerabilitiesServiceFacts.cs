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
    public class PackageVulnerabilitiesServiceFacts
    {
        private Package _packageVulnerable;
        private Package _packageNotVulnerable;
        private PackageVulnerability _vulnerabilityModerate;

        [Fact]
        public void GetsVulnerableStatusOfPackage()
        {
            // Arrange
            Setup();
            var cacheService = new Mock<IPackageVulnerabilitiesCacheService>();
            cacheService.Setup(x => x.GetVulnerabilitiesById(It.IsAny<string>())).Returns((string id) =>
                id == _packageVulnerable.PackageRegistration.Id
                    ? new Dictionary<int, IReadOnlyList<PackageVulnerability>> {
                        { _packageVulnerable.Key, new List<PackageVulnerability> { _vulnerabilityModerate } }
                    } 
                    : null
            );

            var target = new PackageVulnerabilitiesService(cacheService.Object);

            // Act
            var shouldBeVulnerable = target.IsPackageVulnerable(_packageVulnerable);
            var shouldNotBeVulnerable = target.IsPackageVulnerable(_packageNotVulnerable);

            // Assert
            Assert.True(shouldBeVulnerable);
            Assert.False(shouldNotBeVulnerable);
        }

        private void Setup()
        {
            var registrationVulnerable = new PackageRegistration { Id = "Vulnerable" };

            _vulnerabilityModerate = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/5678",
                GitHubDatabaseKey = 5678,
                Severity = PackageVulnerabilitySeverity.Moderate
            };

            var versionRangeModerate = new VulnerablePackageVersionRange
            {
                Vulnerability = _vulnerabilityModerate,
                PackageId = registrationVulnerable.Id,
                PackageVersionRange = "<=1.1.1",
                FirstPatchedPackageVersion = "1.1.2"
            };

            _packageVulnerable = new Package
            {
                Key = 0,
                PackageRegistration = registrationVulnerable,
                Version = "1.0.0",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange> { versionRangeModerate }
            };
            _packageNotVulnerable = new Package
            {
                Key = 4,
                PackageRegistration = new PackageRegistration { Id = "NotVulnerable" },
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>()
            };

            versionRangeModerate.Packages = new List<Package> { _packageVulnerable };
        }
    }
}

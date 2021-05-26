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
        [Fact]
        public void GetsVulnerableStatusOfPackage()
        {
            // Arrange
            var registrationVulnerable = new PackageRegistration { Id = "Vulnerable" };

            var vulnerabilityModerate = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/5678",
                GitHubDatabaseKey = 5678,
                Severity = PackageVulnerabilitySeverity.Moderate
            };

            var versionRangeModerate = new VulnerablePackageVersionRange
            {
                Vulnerability = vulnerabilityModerate,
                PackageVersionRange = "<=1.1.1",
                FirstPatchedPackageVersion = "1.1.2"
            };

            var packageVulnerable = new Package
            {
                Key = 0,
                PackageRegistration = registrationVulnerable,
                Version = "1.0.0",
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange> {versionRangeModerate}
            };
            var packageNotVulnerable = new Package
            {
                Key = 4,
                PackageRegistration = new PackageRegistration { Id = "NotVulnerable" },
                VulnerablePackageRanges = new List<VulnerablePackageVersionRange>()
            }; 
            
            var target = Get<PackageVulnerabilitiesService>();

            // Act
            var shouldBeVulnerable = target.IsPackageVulnerable(packageVulnerable);
            var shouldNotBeVulnerable = target.IsPackageVulnerable(packageNotVulnerable);

            // Assert
            Assert.True(shouldBeVulnerable);
            Assert.False(shouldNotBeVulnerable);
        }
    }
}

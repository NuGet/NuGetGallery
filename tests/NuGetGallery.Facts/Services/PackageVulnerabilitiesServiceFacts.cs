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
        private List<VulnerablePackageVersionRange> _versionRanges;

        [Fact]
        public void GetsVulnerableStatusOfPackage()
        {
            // Arrange
            Setup();
            var entitiesContext = new Mock<IEntitiesContext>();
            entitiesContext.Setup(x => x.Set<VulnerablePackageVersionRange>()).Returns(
                DbMockHelpers.ListToDbSet<VulnerablePackageVersionRange>(_versionRanges));
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(x => x.GetService(typeof(IEntitiesContext))).Returns(entitiesContext.Object);
            var serviceScope = new Mock<IServiceScope>();
            serviceScope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
            serviceScope.Setup(x => x.Dispose()).Verifiable();
            var serviceScopeFactory = new Mock<IServiceScopeFactory>();
            serviceScopeFactory.Setup(x => x.CreateScope()).Returns(serviceScope.Object);
            var cacheService = new PackageVulnerabilitiesCacheService(new Mock<ITelemetryService>().Object);
            cacheService.RefreshCache(serviceScopeFactory.Object);

            var target = new PackageVulnerabilitiesService(cacheService);

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

            var vulnerabilityModerate = new PackageVulnerability
            {
                AdvisoryUrl = "http://theurl/5678",
                GitHubDatabaseKey = 5678,
                Severity = PackageVulnerabilitySeverity.Moderate
            };

            var versionRangeModerate = new VulnerablePackageVersionRange
            {
                Vulnerability = vulnerabilityModerate,
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

            _versionRanges = new List<VulnerablePackageVersionRange> { versionRangeModerate };
        }
    }
}

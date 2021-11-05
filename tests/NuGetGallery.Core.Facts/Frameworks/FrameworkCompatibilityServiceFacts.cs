﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using Xunit;

namespace NuGetGallery.Frameworks
{
    public class FrameworkCompatibilityServiceFacts
    {
        private readonly IFrameworkCompatibilityService _service;
        private readonly IFrameworkCompatibilityProvider CompatibilityProvider = DefaultCompatibilityProvider.Instance;

        public FrameworkCompatibilityServiceFacts()
        {
            _service = new FrameworkCompatibilityService();
        }

        [Fact]
        public void WhenNullPackageFrameworksReturnsNull()
        {
            var result = _service.GetCompatibleFrameworks(null);

            Assert.Null(result);
        }

        [Fact]
        public void WhenEmptyPackageFrameworksReturnsEmptySet()
        {
            var result = _service.GetCompatibleFrameworks(new List<NuGetFramework>());

            Assert.Equal(expected: 0, actual: result.Count);
        }

        [Theory]
        [InlineData("1000")]
        [InlineData("lib")]
        [InlineData("nuget")]
        public void WhenUnsupportedPackageFrameworksReturnsEmptySet(string unsupportedFrameworkName)
        {
            var unsupportedFramework = NuGetFramework.Parse(unsupportedFrameworkName);

            var result = _service.GetCompatibleFrameworks(new List<NuGetFramework>() { unsupportedFramework });

            Assert.True(unsupportedFramework.IsUnsupported);
            Assert.Equal(expected: 0, actual: result.Count);
        }

        [Theory]
        [InlineData("portable-net45+sl4+win8+wp7")]
        [InlineData("portable-net40+sl4")]
        [InlineData("portable-net45+sl5+win8+wpa81+wp8")]
        public void WhenPCLPackageFrameworksReturnsEmptySet(string pclFrameworkName)
        {
            var portableFramework = NuGetFramework.Parse(pclFrameworkName);

            var result = _service.GetCompatibleFrameworks(new List<NuGetFramework>() { portableFramework });

            Assert.True(portableFramework.IsPCL);
            Assert.Equal(expected: 0, actual: result.Count);
        }

        [Theory]
        [InlineData("net5.0", "netcoreapp2.0", "win81")]
        [InlineData("sl4", "netstandard1.2", "netmf")]
        public void ValidPackageFrameworksReturnsFrameworksCompatibleForAtLeastOne(params string[] frameworkNames)
        {
            var frameworks = new List<NuGetFramework>();

            foreach (var frameworkName in frameworkNames)
            {
                frameworks.Add(NuGetFramework.Parse(frameworkName));
            }

            var compatibleFrameworks = _service.GetCompatibleFrameworks(frameworks);

            Assert.True(compatibleFrameworks.Count > 0);

            foreach (var compatibleFramework in compatibleFrameworks)
            {
                var isCompatible = frameworks.Any(f => CompatibilityProvider.IsCompatible(compatibleFramework, f));

                Assert.True(isCompatible);
            }
        }
    }
}

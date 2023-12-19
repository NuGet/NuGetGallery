// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using Xunit;

namespace NuGetGallery.Frameworks
{
    public class FrameworkCompatibilityServiceFacts
    {
        private readonly IFrameworkCompatibilityProvider CompatibilityProvider = DefaultCompatibilityProvider.Instance;

        [Fact]
        public void NullPackageFrameworksThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => FrameworkCompatibilityService.GetCompatibleFrameworks(null));
        }

        [Fact]
        public void EmptyPackageFrameworksReturnsEmptySet()
        {
            var result = FrameworkCompatibilityService.GetCompatibleFrameworks(new List<NuGetFramework>());

            Assert.Empty(result);
        }

        [Fact]
        public void UnknownSupportedPackageReturnsSetWithSameFramework()
        {
            var framework = NuGetFramework.Parse("net45-client");
            var frameworks = new List<NuGetFramework>() { framework };
            var compatible = FrameworkCompatibilityService.GetCompatibleFrameworks(frameworks);

            Assert.False(framework.IsUnsupported);
            Assert.Equal(expected: 1, compatible.Count);
            Assert.Contains(framework, compatible);
        }

        [Theory]
        [InlineData("1000")]
        [InlineData("lib")]
        [InlineData("nuget")]
        public void UnsupportedPackageFrameworksReturnsEmptySet(string unsupportedFrameworkName)
        {
            var unsupportedFramework = NuGetFramework.Parse(unsupportedFrameworkName);

            var result = FrameworkCompatibilityService.GetCompatibleFrameworks(new List<NuGetFramework>() { unsupportedFramework });

            Assert.True(unsupportedFramework.IsUnsupported);
            Assert.Equal(expected: 0, actual: result.Count);
        }

        [Theory]
        [InlineData("portable-net45+sl4+win8+wp7")]
        [InlineData("portable-net40+sl4")]
        [InlineData("portable-net45+sl5+win8+wpa81+wp8")]
        public void PCLPackageFrameworksReturnsEmptySet(string pclFrameworkName)
        {
            var portableFramework = NuGetFramework.Parse(pclFrameworkName);

            var result = FrameworkCompatibilityService.GetCompatibleFrameworks(new List<NuGetFramework>() { portableFramework });

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

            var compatibleFrameworks = FrameworkCompatibilityService.GetCompatibleFrameworks(frameworks);

            Assert.True(compatibleFrameworks.Count > 0);

            foreach (var compatibleFramework in compatibleFrameworks)
            {
                var isCompatible = frameworks.Any(f => CompatibilityProvider.IsCompatible(compatibleFramework, f));

                Assert.True(isCompatible);
            }
        }

        [Theory]
        [InlineData("net5.0", "net5.0", "net6.0", "net7.0", "net5.0-windows")]
        [InlineData("net6.0", "net6.0", "net7.0", "net6.0-android")]
        [InlineData("net6.0-windows", "net7.0-windows")]
        [InlineData("net462", "net472", "net481")]
        [InlineData("netstandard2.1", "net6.0", "net6.0-windows", "netcoreapp3.1", "tizen60")]
        [InlineData("netcoreapp3.0", "netcoreapp3.1", "net5.0", "net7.0-windows")]
        public void ContainsExpectedComputedFrameworks(string assetFramework, params string[] computedFrameworks)
        {
            // Arrange
            var packageFramework = NuGetFramework.Parse(assetFramework);

            var expectedFrameworks = new List<NuGetFramework>();

            foreach (var frameworkName in computedFrameworks)
            {
                expectedFrameworks.Add(NuGetFramework.Parse(frameworkName));
            }

            // Act
            var compatibleFrameworks = FrameworkCompatibilityService.GetCompatibleFrameworks(new List<NuGetFramework>() { packageFramework });

            // Assert
            Assert.True(compatibleFrameworks.Count > 0);

            // check that the list of computed TFMs contains all the specified TFMs
            var containsAllCompatibleFrameworks = expectedFrameworks.All(f => compatibleFrameworks.Contains(f));
            Assert.True(containsAllCompatibleFrameworks);

            // check that every computed TFM included is compatible with the asset TFM
            foreach (var compatibleFramework in compatibleFrameworks)
            {
                var isCompatible = CompatibilityProvider.IsCompatible(compatibleFramework, packageFramework);
                Assert.True(isCompatible);
            }
        }

        [Theory]
        [InlineData("net6.0-windows", "net6.0-windows", "net7.0-windows", "net8.0-windows")]
        [InlineData("net6.0-windows7.0", "net6.0-windows7.0", "net7.0-windows", "net8.0-windows")]
        [InlineData("net7.0-windows", "net7.0-windows", "net8.0-windows")]
        [InlineData("net7.0-windows7.0", "net7.0-windows7.0", "net8.0-windows")]
        [InlineData("net5.0-windows", "net5.0-windows", "net6.0-windows", "net7.0-windows", "net8.0-windows")]
        [InlineData("net5.0-windows7.0", "net5.0-windows7.0", "net6.0-windows", "net7.0-windows", "net8.0-windows")]
        [InlineData("net6.0-ios", "net6.0-ios", "net7.0-ios", "net8.0-ios")]
        [InlineData("net6.0-ios15.0", "net6.0-ios15.0", "net7.0-ios", "net8.0-ios")]
        [InlineData("net6.0-android", "net6.0-android", "net7.0-android", "net8.0-android")]
        [InlineData("net6.0-android31.0", "net6.0-android31.0", "net7.0-android", "net8.0-android")]
        [InlineData("net6.0-windows99.0", "net6.0-windows99.0", "net7.0-windows", "net8.0-windows")]
        public void PlatformVersionsShouldContainAllSpecifiedFrameworks(string platformVersionFramework, params string[] expectedFrameworks)
        {
            // Arrange
            var packageFramework = NuGetFramework.Parse(platformVersionFramework);
            var projectFrameworks = new HashSet<NuGetFramework>();

            foreach (var frameworkName in expectedFrameworks)
            {
                projectFrameworks.Add(NuGetFramework.Parse(frameworkName));
            }

            // Act
            var compatibleFrameworks = FrameworkCompatibilityService.GetCompatibleFrameworks(new HashSet<NuGetFramework>() { packageFramework });

            // Assert
            Assert.Equal(expectedFrameworks.Length, compatibleFrameworks.Count);

            var containsAllCompatibleFrameworks = compatibleFrameworks.All(cf => projectFrameworks.Contains(cf));
            Assert.True(containsAllCompatibleFrameworks);
        }

        [Theory]
        [InlineData(new string[] { "net5.0-windows7.0", "net6.0-windows" },
                    new string[] { "net5.0-windows7.0", "net6.0-windows", "net7.0-windows", "net8.0-windows" })]
        [InlineData(new string[] { "net5.0-windows", "net6.0-windows7.0" },
                    new string[] { "net5.0-windows", "net6.0-windows", "net7.0-windows", "net8.0-windows", "net6.0-windows7.0" })]
        [InlineData(new string[] { "net5.0-windows7.0", "net6.0-windows7.0" },
                    new string[] { "net5.0-windows7.0", "net6.0-windows", "net7.0-windows", "net8.0-windows", "net6.0-windows7.0" })]
        [InlineData(new string[] { "net5.0-windows99.0", "net6.0-windows99.0" },
                    new string[] { "net5.0-windows99.0", "net6.0-windows", "net7.0-windows", "net8.0-windows", "net6.0-windows99.0" })]
        public void MultiplePlatformVersionCasesShouldContainAllSpecifiedFrameworks(string[] platformVersionFrameworks, string[] expectedFrameworks)
        {
            // Arrange
            var packageFrameworks = new HashSet<NuGetFramework>();
            foreach (var frameworkName in platformVersionFrameworks)
            {
                packageFrameworks.Add(NuGetFramework.Parse(frameworkName));
            }

            var expectedComputedFrameworks = new HashSet<NuGetFramework>();
            foreach (var frameworkName in expectedFrameworks)
            {
                expectedComputedFrameworks.Add(NuGetFramework.Parse(frameworkName));
            }

            // Act
            var compatibleFrameworks = FrameworkCompatibilityService.GetCompatibleFrameworks(packageFrameworks);

            // Assert
            Assert.Equal(expectedFrameworks.Length, compatibleFrameworks.Count);

            var containsAllCompatibleFrameworks = compatibleFrameworks.All(cf => expectedComputedFrameworks.Contains(cf));
            Assert.True(containsAllCompatibleFrameworks);
        }
    }
}

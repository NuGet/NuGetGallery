// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using Xunit;

namespace NuGetGallery
{
    public class SemVerLevelFacts
    {
        [Fact]
        public void AssertUnknownKeyNotChanged()
        {
            Assert.Null(SemVerLevel.UnknownKey);
        }

        [Fact]
        public void AssertSemVer2KeyNotChanged()
        {
            Assert.Equal(2, SemVerLevel.SemVer2Key);
        }

        public class TheForPackageMethod
        {
            [Fact]
            public void ThrowsArgumentNullWhenOriginalVersionStringIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => SemVerLevel.ForPackage(null, null));
            }

            [Theory]
            [InlineData("1")]
            [InlineData("1.0")]
            [InlineData("1.0.0")]
            [InlineData("1.0.0-alpha")]
            [InlineData("1.0.0-alpha-01")]
            [InlineData("1.0.0.0")]
            [InlineData("1.0.0.0-alpha")]
            [InlineData("1.0.0.0-alpha-01")]
            public void ReturnsUnknownForNonSemVer2CompliantPackages(string originalVersionString)
            {
                // Arrange
                var nugetVersion = NuGetVersion.Parse(originalVersionString);

                // Act
                var key = SemVerLevel.ForPackage(nugetVersion, null);

                // Assert
                Assert.Equal(SemVerLevel.UnknownKey, key);
            }

            [Theory]
            [InlineData("1.0.0-alpha.1")]
            [InlineData("1.0.0-alpha.1+metadata")]
            [InlineData("1.0.0-alpha+metadata")]
            [InlineData("1.0.0+metadata")]
            public void ReturnsSemVer2ForSemVer2CompliantPackagesThatAreNotSemVer1Compliant(string originalVersionString)
            {
                // Arrange
                var nugetVersion = NuGetVersion.Parse(originalVersionString);

                // Act
                var key = SemVerLevel.ForPackage(nugetVersion, null);

                // Assert
                Assert.Equal(SemVerLevel.SemVer2Key, key);
            }

            [Theory]
            [InlineData("(1.0.0-alpha.1, )")]
            [InlineData("[1.0.0-alpha.1+metadata]")]
            [InlineData("1.0.0-alpha+metadata")]
            [InlineData("[1.0, 2.0.0+metadata)")]
            [InlineData("[1.0+metadata, 2.0.0+metadata)")]
            public void ReturnsSemVer2ForSemVer2CompliantDependenciesThatAreNotSemVer1Compliant(string versionSpec)
            {
                // Arrange
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var dependency = new PackageDependency { VersionSpec = versionSpec };

                // Act
                var key = SemVerLevel.ForPackage(packageVersion, new[] { dependency });

                // Assert
                Assert.Equal(SemVerLevel.SemVer2Key, key);
            }

            [Theory]
            [InlineData("(1.0.0-alpha, )")]
            [InlineData("[1.0.0-alpha]")]
            [InlineData("1.0.0")]
            [InlineData("1.0.0-alpha")]
            [InlineData("[1.0-alpha, 2.0.0)")]
            [InlineData("[1.0, 2.0.0-alpha)")]
            public void ReturnsUnknownForNonSemVer2CompliantDependenciesThatAreNotSemVer1Compliant(string versionSpec)
            {
                // Arrange
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var dependency = new PackageDependency { VersionSpec = versionSpec };

                // Act
                var key = SemVerLevel.ForPackage(packageVersion, new[] { dependency });

                // Assert
                Assert.Equal(SemVerLevel.UnknownKey, key);
            }
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGet.Versioning;
using Xunit;

namespace NuGetGallery
{
    public class SemVerLevelKeyFacts
    {
        [Fact]
        public void AssertUnknownKeyNotChanged()
        {
            Assert.Null(SemVerLevelKey.Unknown);
        }

        [Fact]
        public void AssertSemVer2KeyNotChanged()
        {
            Assert.Equal(2, SemVerLevelKey.SemVer2);
        }

        public class TheForPackageMethod
        {
            [Fact]
            public void ThrowsArgumentNullWhenOriginalVersionStringIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => SemVerLevelKey.ForPackage(null, null));
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
                var key = SemVerLevelKey.ForPackage(nugetVersion, null);

                // Assert
                Assert.Equal(SemVerLevelKey.Unknown, key);
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
                var key = SemVerLevelKey.ForPackage(nugetVersion, null);

                // Assert
                Assert.Equal(SemVerLevelKey.SemVer2, key);
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
                var key = SemVerLevelKey.ForPackage(packageVersion, new[] { dependency });

                // Assert
                Assert.Equal(SemVerLevelKey.SemVer2, key);
            }

            [Theory]
            [InlineData("(1.0.0-alpha, )")]
            [InlineData("[1.0.0-alpha]")]
            [InlineData("1.0.0")]
            [InlineData("1.0.0-alpha")]
            [InlineData("[1.0-alpha, 2.0.0)")]
            [InlineData("[1.0, 2.0.0-alpha)")]
            [InlineData(null)]
            public void ReturnsUnknownForNonSemVer2CompliantDependenciesThatAreNotSemVer1Compliant(string versionSpec)
            {
                // Arrange
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var dependency = new PackageDependency { VersionSpec = versionSpec };

                // Act
                var key = SemVerLevelKey.ForPackage(packageVersion, new[] { dependency });

                // Assert
                Assert.Equal(SemVerLevelKey.Unknown, key);
            }
        }

        public class TheForSemVerLevelMethod
        {
            [Theory]
            [InlineData("")]
            [InlineData("this.is.not.a.version.string")]
            [InlineData("1.0.0-alpha.01")] // no leading zeros in numeric identifiers
            [InlineData("1.0.0")]
            [InlineData("2.0.0-alpha")]
            public void DefaultsToUnknownKeyWhenVersionStringIsInvalidOrLowerThanVersion200(string semVerLevel)
            {
                // Act
                var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(semVerLevel);

                // Assert
                Assert.Equal(SemVerLevelKey.Unknown, semVerLevelKey);
            }

            [Theory]
            [InlineData("3.0.0")]
            [InlineData("3.0.0-alpha")]
            [InlineData("2.0.0")]
            [InlineData("2.0.1")]
            public void ReturnsSemVer2KeyWhenVersionStringAtLeastVersion200(string semVerLevel)
            {
                // Act
                var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(semVerLevel);

                // Assert
                Assert.Equal(SemVerLevelKey.SemVer2, semVerLevelKey);
            }

            [Fact]
            public void DefaultsToUnknownKeyWhenVersionStringIsNull()
            {
                // Act
                var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(null);

                // Assert
                Assert.Equal(SemVerLevelKey.Unknown, semVerLevelKey);
            }
        }

        public class TheIsCompliantWithSemVerLevelPredicateMethod
        {
            [Theory]
            // Versions higher than SemVer v2.0.0
            [InlineData("3.0.0")]
            [InlineData("3.0.0-alpha")]
            [InlineData("2.0.0")]
            [InlineData("2.0.1")]
            // Versions lower than SemVer v2.0.0
            [InlineData("2.0.0-alpha")] // no leading zeros in numeric identifiers
            [InlineData("1.0.1")]
            // Invalid/undefined versions
            [InlineData(null)]
            [InlineData("this.is.not.a.valid.version.string")]
            [InlineData("2.0.0-alpha.01")] // no leading zeros in numeric identifiers
            [InlineData("-2.0.1")]
            public void UnknownKey_IsCompliantWithAnySemVerLevelString(string semVerLevel)
            {
                AssertPackageIsComplianceWithSemVerLevel(SemVerLevelKey.Unknown, semVerLevel, shouldBeCompliant: true);
            }

            [Theory]
            // Versions higher than SemVer v2.0.0
            [InlineData("3.0.0")]
            [InlineData("3.0.0-alpha")]
            [InlineData("2.0.0")]
            [InlineData("2.0.1")]
            public void SemVer2Key_IsCompliantWithSemVerLevel200OrHigher(string semVerLevel)
            {
                AssertPackageIsComplianceWithSemVerLevel(SemVerLevelKey.SemVer2, semVerLevel, shouldBeCompliant: true);
            }

            [Theory]
            // Invalid versions
            [InlineData("this.is.not.a.valid.version.string")]
            [InlineData("2.0.0-alpha.01")] // no leading zeros in numeric identifiers
            [InlineData("-2.0.1")]
            public void SemVer2Key_IsNotCompliantWithInvalidVersionStrings(string semVerLevel)
            {
                AssertPackageIsComplianceWithSemVerLevel(SemVerLevelKey.SemVer2, semVerLevel, shouldBeCompliant: false);
            }


            [Theory]
            // Versions lower than SemVer v2.0.0
            [InlineData(null)]
            [InlineData("2.0.0-alpha")] // no leading zeros in numeric identifiers
            [InlineData("1.0.1")]
            public void SemVer2Key_IsNotCompliantWithVersionStringLowerThanSemVer2(string semVerLevel)
            {
                AssertPackageIsComplianceWithSemVerLevel(SemVerLevelKey.SemVer2, semVerLevel, shouldBeCompliant: false);
            }

            [Fact]
            public void SemVer2Key_IsNotCompliantWithUnknownSemVerLevel()
            {
                AssertPackageIsComplianceWithSemVerLevel(SemVerLevelKey.SemVer2, semVerLevel: null, shouldBeCompliant: false);
            }

            private static void AssertPackageIsComplianceWithSemVerLevel(int? packageSemVerLevelKey, string semVerLevel, bool shouldBeCompliant)
            {
                var package = new Package { SemVerLevelKey = packageSemVerLevelKey };
                var compiledFunction = SemVerLevelKey.IsPackageCompliantWithSemVerLevelPredicate(semVerLevel).Compile();

                Assert.Equal(shouldBeCompliant, compiledFunction(package));
            }
        }

        public class TheIsUnknownPredicateMethod
        {
            [Theory]
            [InlineData(null, true)]
            [InlineData(1, false)]
            [InlineData(2, false)]
            [InlineData(3, false)]
            public void IsUnknown(int? semVerLevelKey, bool expected)
            {
                var package = new Package { SemVerLevelKey = semVerLevelKey };
                var compiledFunction = SemVerLevelKey.IsUnknownPredicate().Compile();

                var actual = compiledFunction(package);

                Assert.Equal(expected, actual);
            }
        }
    }
}
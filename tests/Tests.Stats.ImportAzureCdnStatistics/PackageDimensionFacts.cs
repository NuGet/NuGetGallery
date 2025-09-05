// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.ImportAzureCdnStatistics;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class PackageDimensionFacts
    {
        [Theory]
        [InlineData("abc", "1.0.0", "ABC", "1.0.0", true)] // Lowercase and uppercase are equal
        [InlineData("İ", "1.0.0", "i", "1.0.0", true)] // Turkish capital i is like regular i
        [InlineData("abc", "1.0.0", "cbd", "1.0.0", false)] // Different package ids
        [InlineData("abc", "1.0.0", "abc", "2.0.0", false)] // Different package versions
        public void ComparesPackageDimensionsCorrectly(string packageId1, string version1, string packageId2, string version2, bool expectedAreEqual)
        {
            // Arrange
            var dimension1 = new PackageDimension(packageId1, version1);
            var dimension2 = new PackageDimension(packageId2, version2);

            // Act
            bool areEqualHashCode = dimension1.GetHashCode() == dimension2.GetHashCode();
            bool areEqualEquals = dimension1.Equals(dimension2);

            // Assert
            Assert.Equal(expectedAreEqual, areEqualHashCode);
            Assert.Equal(expectedAreEqual, areEqualEquals);
        }
    }
}
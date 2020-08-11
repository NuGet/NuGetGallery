// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Helpers;
using Xunit;

namespace CatalogTests.Helpers
{
    public class NuGetVersionUtilityTests
    {
        [Theory]
        [InlineData("1.0.0-alpha", "1.0.0-alpha")]
        [InlineData("1.0.0-alpha.1", "1.0.0-alpha.1")]
        [InlineData("1.0.0-alpha+githash", "1.0.0-alpha")]
        [InlineData("1.0.0.0", "1.0.0")]
        [InlineData("invalid", "invalid")]
        public void NormalizeVersion(string input, string expected)
        {
            // Arrange & Act
            var actual = NuGetVersionUtility.NormalizeVersion(input);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("[1.0.0-alpha, )", "[1.0.0-alpha, )")]
        [InlineData("1.0.0-alpha.1", "[1.0.0-alpha.1, )")]
        [InlineData("[1.0.0-alpha+githash, )", "[1.0.0-alpha, )")]
        [InlineData("[1.0, 2.0]", "[1.0.0, 2.0.0]")]
        [InlineData("invalid", "invalid")]
        public void NormalizeVersionRange(string input, string expected)
        {
            // Arrange
            var defaultValue = input;

            // Arrange
            var actual = NuGetVersionUtility.NormalizeVersionRange(input, defaultValue);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizeVersionRange_UsesDifferentDefault()
        {
            // Arrange
            var input = "invalid";
            var defaultValue = "(, )";

            // Act
            var actual = NuGetVersionUtility.NormalizeVersionRange(input, defaultValue);

            // Assert
            Assert.Equal(defaultValue, actual);
        }

        [Theory]
        [InlineData("1.0.0-alpha.1", "1.0.0-alpha.1")]
        [InlineData("1.0.0-alpha+githash", "1.0.0-alpha+githash")]
        [InlineData("1.0.0.0", "1.0.0")]
        [InlineData("invalid", "invalid")]
        public void GetFullVersionString(string input, string expected)
        {
            // Arrange & Act
            var actual = NuGetVersionUtility.GetFullVersionString(input);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}

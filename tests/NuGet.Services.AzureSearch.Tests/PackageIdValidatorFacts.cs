// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    // TODO: Delete this copy of the PackageIdValidator.
    // Tracked by: https://github.com/NuGet/Engineering/issues/3669
    // Forked from: https://github.com/NuGet/NuGet.Client/blob/18863da5be3dc8c7315f4416df1bc9ef96cb7446/test/NuGet.Core.Tests/NuGet.Packaging.Test/PackageIdValidatorTest.cs#L8
    public class PackageIdValidatorTest
    {
        [Fact]
        public void EmptyIsNotValid()
        {
            // Arrange
            string packageId = "";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void NullThrowsException()
        {
            // Arrange
            string packageId = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(paramName: "packageId",
                testCode: () => PackageIdValidator.IsValidPackageIdWithTimeout(packageId));
        }

        [Fact]
        public void AlphaNumericIsValid()
        {
            // Arrange
            string packageId = "42This1Is4You";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void MultipleDotSeparatorsAllowed()
        {
            // Arrange
            string packageId = "I.Like.Writing.Unit.Tests";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void NumbersAndWordsDotSeparatedAllowd()
        {
            // Arrange
            string packageId = "1.2.3.4.Uno.Dos.Tres.Cuatro";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void UnderscoreDotAndDashSeparatorsAreValid()
        {
            // Arrange
            string packageId = "Nu_Get.Core-IsCool";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void NonAlphaNumericUnderscoreDotDashIsInvalid()
        {
            // Arrange
            string packageId = "ILike*Asterisks";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ConsecutiveSeparatorsNotAllowed()
        {
            // Arrange
            string packageId = "I_.Like.-Separators";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void StartingWithSeparatorsNotAllowed()
        {
            // Arrange
            string packageId = "-StartWithSeparator";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void EndingWithSeparatorsNotAllowed()
        {
            // Arrange
            string packageId = "StartWithSeparator.";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void DotToolsIsNotAllowed()
        {
            // Arrange
            string packageId = ".tools";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void IsValidPackageId_PackageIdWithTwoUnderscores_Success()
        {
            // Arrange
            string packageId = "Hello__World";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageIdWithTimeout(packageId);

            // Assert
            Assert.True(isValid);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Packaging
{
    public class PackageIdValidatorTest
    {
        [Theory]
        [InlineData("42This1Is4You")]
        [InlineData("I.Like.Writing.Unit.Tests")]
        [InlineData("1.2.3.4.Uno.Dos.Tres.Cuatro")]
        [InlineData("Nu_Get.Core-IsCool")]
        public void ValidatePackageIdWithValidIdReturnsTrue(string packageId)
        {
            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void NullThrowsException()
        {
            // Arrange
            string packageId = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>("packageId",
                () => PackageIdValidator.IsValidPackageId(packageId));
        }

        [Theory]
        [InlineData("")]
        [InlineData("ILike*Asterisks")]
        [InlineData("I_.Like.-Separators")]
        [InlineData("-StartWithSeparator")]
        [InlineData("EndWithSeparator.")]
        [InlineData("EndsWithHyphen-")]
        [InlineData("$id$")]
        [InlineData("Contains#Invalid$Characters!@#$%^&*")]
        [InlineData("Contains#Invalid$Characters!@#$%^&*EndsOnValidCharacter")]
        public void ValidatePackageIdInvalidIdReturnsFalse(string packageId)
        {
            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Theory()]
        [InlineData("  Invalid  . Woo   .")]
        [InlineData("(/.__.)/ \\(.__.\\)")]
        public void ValidatePackageIdInvalidIdThrows(string packageId)
        {
            // Act & Assert
            Exception thrownException = null;
            try
            {
                PackageIdValidator.ValidatePackageId(packageId);
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            Assert.NotNull(thrownException);
            Assert.Equal("The package ID '" + packageId + "' contains invalid characters. Package ID can only contain alphanumeric characters, hyphens, underscores, and periods.", thrownException.Message);
        }

        [Theory]
        [InlineData(129)]
        [InlineData(130)]
        [InlineData(200)]
        public void IdExceedingMaxLengthThrows(int idTestLength)
        {
            // Arrange
            string packageId = new string('d', idTestLength);

            // Act && Assert
            Exception thrownException = null;
            try
            {
                PackageIdValidator.ValidatePackageId(packageId);
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            Assert.NotNull(thrownException);
            Assert.Equal("Id must not exceed " + Constants.MaxPackageIdLength + " characters.", thrownException.Message);
        }

        [Fact]
        public void IsAsciiPackageId_WithNull_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => PackageIdValidator.IsAsciiOnlyPackageId(null));
            Assert.Equal("packageId", exception.ParamName);
        }

        [Fact]
        public void IsAsciiPackageId_WithTemplateId_ReturnsFalse()
        {
            // Arrange & Act
            var result = PackageIdValidator.IsAsciiOnlyPackageId("$id$");

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("package")]
        [InlineData("package.id")]
        [InlineData("package-id")]
        [InlineData("package_id")]
        [InlineData("package.id-with_various.symbols")]
        [InlineData("123456789")]
        public void IsAsciiPackageId_WithAsciiOnlyCharacters_ReturnsTrue(string packageId)
        {
            // Arrange & Act
            var result = PackageIdValidator.IsAsciiOnlyPackageId(packageId);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("packageá")]
        [InlineData("包")]
        [InlineData("пакет")]
        [InlineData("패키지")]
        [InlineData("الحزمة")]
        [InlineData("package™")]
        [InlineData("package©")]
        [InlineData("package£")]
        [InlineData("elsökning")]
        [InlineData("件")]
        [InlineData("valoniα")]
        public void IsAsciiPackageId_WithUnicodeCharacters_ReturnsFalse(string packageId)
        {
            // Arrange & Act
            var result = PackageIdValidator.IsAsciiOnlyPackageId(packageId);

            // Assert
            Assert.False(result);
        }
    }
}

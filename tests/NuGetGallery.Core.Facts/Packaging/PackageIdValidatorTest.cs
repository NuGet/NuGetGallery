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

        [Theory]
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
            Assert.Equal("The package ID '" + packageId + "' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.", thrownException.Message);
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
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Moq;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class ValidationHelperFacts
    {
        public class HasDuplicatedEntriesMethod
        {
            [Theory]
            [InlineData("duplicatedFile.txt", "duplicatedFile.txt")]
            [InlineData("./temp/duplicatedFile.txt", "./temp/duplicatedFile.txt")]
            [InlineData("./temp/duplicatedFile.txt", "./temp\\duplicatedFile.txt")]
            [InlineData("./temp\\duplicatedFile.txt", "./temp\\duplicatedFile.txt")]
            [InlineData("duplicatedFile.txt", "duplicatedFile.TXT")]
            [InlineData("./duplicatedFile.txt", "duplicatedFile.txt")]
            [InlineData("./duplicatedFile.txt", "/duplicatedFile.txt")]
            [InlineData("/duplicatedFile.txt", "duplicatedFile.txt")]
            [InlineData(".\\duplicatedFile.txt", "./duplicatedFile.txt")]
            public void WithDuplicatedEntries_ReturnsFalse(params string[] entryNames)
            {
                // Arrange
                var package = GeneratePackage(entryNames: entryNames);

                // Act
                var hasDuplicatedEntries = ValidationHelper.HasDuplicatedEntries(package.Object);

                // Assert
                Assert.True(hasDuplicatedEntries);
            }

            [Theory]
            [InlineData("noDuplicatedFile.txt", "./temp/noDuplicatedFile.txt")]
            [InlineData("./temp1/noDuplicatedFile.txt", "./temp2/noDuplicatedFile.txt")]
            [InlineData("./temp1/noDuplicatedFile.txt", "./temp1/noDuplicatedFile.css")]
            [InlineData("./temp1/noDuplicatedFile.txt", "./temp1\\noDuplicatedFile.css")]
            public void WithNoDuplicatedEntries_ReturnsTrue(params string[] entryNames)
            {
                // Arrange
                var package = GeneratePackage(entryNames: entryNames);

                // Act
                var hasDuplicatedEntries = ValidationHelper.HasDuplicatedEntries(package.Object);

                // Assert
                Assert.False(hasDuplicatedEntries);
            }

            private Mock<TestPackageReader> GeneratePackage(IReadOnlyList<string> entryNames)
            {
                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(entryNames: entryNames);
                return PackageServiceUtility.CreateNuGetPackage(packageStream);
            }
        }
    }
}

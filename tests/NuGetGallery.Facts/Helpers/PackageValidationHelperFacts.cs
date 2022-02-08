// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class PackageValidationHelperFacts
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
                var package = PackageValidationHelperFacts.GeneratePackage(entryNames: entryNames);

                // Act
                var hasDuplicatedEntries = PackageValidationHelper.HasDuplicatedEntries(package.Object);

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
                var package = PackageValidationHelperFacts.GeneratePackage(entryNames: entryNames);

                // Act
                var hasDuplicatedEntries = PackageValidationHelper.HasDuplicatedEntries(package.Object);

                // Assert
                Assert.False(hasDuplicatedEntries);
            }
        }

        public class GetNormalizedEntryPathsMethod
        {
            [Theory]
            [InlineData("./net50\\file.dll", "net50/file.dll")]
            [InlineData("\\netstandard10\\file.dll", "netstandard10/file.dll")]
            [InlineData("\\\\net472", "net472")]
            public void AlwaysReturnsCorrectPath(string path, string correctPath)
            {
                // Arrange
                var paths = new string[] { path };
                var package = PackageValidationHelperFacts.GeneratePackage(entryNames: paths);

                // Act
                var normalizedPaths = PackageValidationHelper.GetNormalizedEntryPaths(package.Object).Skip(1);

                // Assert
                Assert.Equal(normalizedPaths.First(), correctPath);
            }
        }

        public static Mock<TestPackageReader> GeneratePackage(IReadOnlyList<string> entryNames)
        {
            var packageStream = PackageServiceUtility.CreateNuGetPackageStream(entryNames: entryNames);
            return PackageServiceUtility.CreateNuGetPackage(packageStream);
        }
    }
}
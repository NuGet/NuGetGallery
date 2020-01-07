// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGetGallery.Packaging
{
    public class PackageMetadataFacts
    {
        private static IEnumerable<string> TextValues = new[]
        {
            string.Empty,
            " \t ",
            "some value",
            "\tfoo\r\n",
            "&#60;",
            "<inner>foo</inner>",
            "<inner />",
        };
        private static IEnumerable<string> BooleanNames = new[]
        {
            "developmentDependency",
            "requireLicenseAcceptance",
            "serviceable",
        };
        private static IEnumerable<string> RestrictedNames = new[]
        {
            "created",
            "dependencyGroups",
            "isPrerelease",
            "lastEdited",
            "listed",
            "packageEntries",
            "packageHash",
            "packageHashAlgorithm",
            "packageSize",
            "published",
            "supportedFrameworks",
            "verbatimVersion",
        };

        public static IEnumerable<object[]> BooleanNameData => BooleanNames
            .Select(n => new object[] { n });

        public static IEnumerable<object[]> RestrictedNameData = RestrictedNames
            .Select(n => new object[] { n });

        public static IEnumerable<object[]> BooleanNameAndValueData => BooleanNames
            .SelectMany(n => TextValues.Select(v => new object[] { n, v }));

        public static IEnumerable<object[]> RestrictedNameAndValueData = RestrictedNames
            .SelectMany(n => TextValues.Select(v => new object[] { n, v }));

        public static IEnumerable<object[]> UnofficialNameAndValueData = new[] { "foo" }
            .SelectMany(n => TextValues.Select(v => new object[] { n, v }));

        [Theory]
        [MemberData(nameof(BooleanNameAndValueData))]
        public void RejectsInvalidBooleanValue(string name, string value)
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithMetadataElementName(name, value);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(nuspec, strict: false));
            Assert.Equal($"The package manifest contains an invalid boolean value for metadata element: '{name}'. The value should be 'true' or 'false'.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(BooleanNameData))]
        public void RejectsEmptyBooleanValue(string name)
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithMetadataElementNameAndEmptyValue(name);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(nuspec, strict: false));
            Assert.Equal($"The package manifest contains an invalid boolean value for metadata element: '{name}'. The value should be 'true' or 'false'.", ex.Message);
        }

        [Theory]
        [InlineData("Bad package type")]
        [InlineData("Bad--packageType")]
        [InlineData("Bad..packageType")]
        [InlineData("Bad!!packageType")]
        public void RejectsInvalidPackageTypeName(string name)
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithPackageTypes("Dependency", name);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(nuspec, strict: false));
            Assert.Equal($"The package manifest contains an invalid package type name: '{name}'", ex.Message);
        }

        [Theory]
        [InlineData("goodpackagetype")]
        [InlineData("GoodPackageType")]
        [InlineData("    GoodPackageType    ")]
        [InlineData("good__packageType")]
        public void RejectsValidPackageTypeName(string name)
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithPackageTypes(name);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act
            var metadata = PackageMetadata.FromNuspecReader(nuspec, strict: false);

            // Assert
            var packageType = Assert.Single(metadata.GetPackageTypes());
            Assert.Equal(name.Trim(), packageType.Name);
            Assert.Equal(new Version(0, 0), packageType.Version);
        }

        [Theory]
        [MemberData(nameof(RestrictedNameAndValueData))]
        public void RejectsRestrictedElementNames(string name, string value)
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithMetadataElementName(name, value);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(nuspec, strict: false));
            Assert.Equal($"The package manifest contains invalid metadata elements: '{name}'", ex.Message);
        }

        [Theory]
        [MemberData(nameof(RestrictedNameData))]
        public void RejectsRestrictedElementNamesWithEmptyValue(string name)
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithMetadataElementNameAndEmptyValue(name);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(nuspec, strict: false));
            Assert.Equal($"The package manifest contains invalid metadata elements: '{name}'", ex.Message);
        }

        [Theory]
        [MemberData(nameof(UnofficialNameAndValueData))]
        public void AllowsUnrestrictedButUnofficialElementNames(string name, string value)
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithMetadataElementName(name, value);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act
            var packageMetadata = PackageMetadata.FromNuspecReader(
                nuspec,
                strict: true);

            // Assert
            Assert.Equal("TestPackage", packageMetadata.Id);
            Assert.Equal(NuGetVersion.Parse("0.0.0.1"), packageMetadata.Version);
        }

        [Fact]
        public void CanReadBasicMetadataProperties()
        {
            // Arrange
            var packageStream = CreateTestPackageStream();
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act
            var packageMetadata = PackageMetadata.FromNuspecReader(
                nuspec,
                strict: true);

            // Assert
            Assert.Equal("TestPackage", packageMetadata.Id);
            Assert.Equal(NuGetVersion.Parse("0.0.0.1"), packageMetadata.Version);
            Assert.Equal("Package A", packageMetadata.Title);
            Assert.Equal(2, packageMetadata.Authors.Count);
            Assert.Equal("ownera, ownerb", packageMetadata.Owners);
            Assert.False(packageMetadata.RequireLicenseAcceptance);
            Assert.Equal("package A description.", packageMetadata.Description);
            Assert.Equal("en-US", packageMetadata.Language);
            Assert.Equal("http://www.nuget.org/", packageMetadata.ProjectUrl.ToString());
            Assert.Equal("http://www.nuget.org/", packageMetadata.IconUrl.ToString());
            Assert.Equal("http://www.nuget.org/", packageMetadata.LicenseUrl.ToString());
            Assert.Equal("https://github.com/NuGet/NuGetGallery", packageMetadata.RepositoryUrl.ToString());
            Assert.Equal("git", packageMetadata.RepositoryType);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ThrowsWhenInvalidMinClientVersion(bool strict)
        {
            var packageStream = CreateTestPackageStreamWithInvalidMinClientVersion();
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PackageMetadata.FromNuspecReader(nuspec, strict));
            Assert.Equal("'bad' is not a valid version string.\r\nParameter name: value", ex.Message);
        }

        [Fact]
        public void ThrowsPackagingExceptionWhenInvalidDependencyVersionRangeDetected()
        {
            var packageStream = CreateTestPackageStreamWithInvalidDependencyVersion();
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(
                nuspec,
                strict: true));
        }

        [Fact]
        public void ThrowsPackagingExceptionWhenEmptyAndNonEmptyDuplicateMetadataElementsDetected()
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithDuplicateEmptyAndNonEmptyMetadataElements();
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(
                nuspec,
                strict: true));
            Assert.Equal(
                "The package manifest contains duplicate metadata elements: 'title', 'authors', 'owners', 'description', 'language', 'foo', 'releaseNotes'",
                ex.Message);
        }

        [Fact]
        public void ThrowsForEmptyAndNonEmptyDuplicatesWhenDuplicateMetadataElementsDetectedAndParsingIsNotStrict()
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithDuplicateEmptyAndNonEmptyMetadataElements();
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PackageMetadata.FromNuspecReader(
                nuspec,
                strict: false));
            Assert.Equal(
                "An item with the same key has already been added.",
                ex.Message);
        }

        [Fact]
        public void ThrowsPackagingExceptionWhenEmptyDuplicateMetadataElementsDetected()
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithDuplicateEmptyMetadataElements();
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(
                nuspec,
                strict: true));
            Assert.Equal(
                "The package manifest contains duplicate metadata elements: 'title', 'authors', 'description', 'releaseNotes'",
                ex.Message);
        }

        [Fact]
        public void ThrowsForEmptyDuplicatesWhenDuplicateMetadataElementsDetectedAndParsingIsNotStrict()
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithDuplicateEmptyMetadataElements();
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act
            var packageMetadata = PackageMetadata.FromNuspecReader(
                nuspec,
                strict: false);

            // Assert
            Assert.Equal("TestPackage", packageMetadata.Id);
            Assert.Equal(NuGetVersion.Parse("0.0.0.1"), packageMetadata.Version);
            Assert.Equal("Package A", packageMetadata.Title);
            Assert.Equal(new[] { "authora", "authorb" }, packageMetadata.Authors);
            Assert.Equal("package A description.", packageMetadata.Description);
            Assert.Null(packageMetadata.ReleaseNotes);
        }

        [Fact]
        public void DoesNotThrowWhenInvalidDependencyVersionRangeDetectedAndParsingIsNotStrict()
        {
            var packageStream = CreateTestPackageStreamWithInvalidDependencyVersion();
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act
            var packageMetadata = PackageMetadata.FromNuspecReader(
                nuspec,
                strict: false);

            // Assert
            Assert.Equal("TestPackage", packageMetadata.Id);
            Assert.Equal(NuGetVersion.Parse("0.0.0.1"), packageMetadata.Version);
            Assert.Equal("Package A", packageMetadata.Title);
            Assert.Equal(2, packageMetadata.Authors.Count);
            Assert.Equal("ownera, ownerb", packageMetadata.Owners);
            Assert.False(packageMetadata.RequireLicenseAcceptance);
            Assert.Equal("package A description.", packageMetadata.Description);
            Assert.Equal("en-US", packageMetadata.Language);
            Assert.Equal("http://www.nuget.org/", packageMetadata.ProjectUrl.ToString());
            Assert.Equal("http://www.nuget.org/", packageMetadata.IconUrl.ToString());
            Assert.Equal("http://www.nuget.org/", packageMetadata.LicenseUrl.ToString());
            var dependencyGroup = Assert.Single(packageMetadata.GetDependencyGroups());
            var dependency = Assert.Single(dependencyGroup.Packages);
            Assert.Equal("SampleDependency", dependency.Id);
            Assert.Equal(VersionRange.All, dependency.VersionRange);
        }

        private static Stream CreateTestPackageStream()
        {
            return CreateTestPackageStream(@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                        <title>Package A</title>
                        <authors>authora, authorb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <projectUrl>http://www.nuget.org/</projectUrl>
                        <iconUrl>http://www.nuget.org/</iconUrl>
                        <licenseUrl>http://www.nuget.org/</licenseUrl>
                        <dependencies />
                        <repository type=""git"" url=""https://github.com/NuGet/NuGetGallery"" commit=""33a34174353a8bf64ab0ee0373936010e948d59d"" branch=""dev"" />
                      </metadata>
                    </package>");
        }

        private static Stream CreateTestPackageStreamWithMetadataElementName(string metadataName, string value = "some value")
        {
            return CreateTestPackageStream($@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                        <{metadataName}>{value}</{metadataName}>
                      </metadata>
                    </package>");
        }

        private static Stream CreateTestPackageStreamWithMetadataElementNameAndEmptyValue(string metadataName)
        {
            return CreateTestPackageStream($@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                        <{metadataName} />
                      </metadata>
                    </package>");
        }

        private static Stream CreateTestPackageStreamWithInvalidMinClientVersion()
        {
            return CreateTestPackageStream(@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata minClientVersion=""bad"">
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                      </metadata>
                    </package>");
        }

        private static Stream CreateTestPackageStreamWithInvalidDependencyVersion()
        {
            return CreateTestPackageStream(@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                        <title>Package A</title>
                        <authors>authora, authorb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <projectUrl>http://www.nuget.org/</projectUrl>
                        <iconUrl>http://www.nuget.org/</iconUrl>
                        <licenseUrl>http://www.nuget.org/</licenseUrl>
                        <dependencies>
                          <dependency id=""SampleDependency"" version=""$version$""/>
                        </dependencies>
                      </metadata>
                    </package>");
        }

        private static Stream CreateTestPackageStreamWithDuplicateEmptyMetadataElements()
        {
            return CreateTestPackageStream(@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                        <title>Package A</title>
                        <title />
                        <authors>authora, authorb</authors>
                        <authors></authors>
                        <description>

                        </description>
                        <description>package A description.</description>
                        <releaseNotes></releaseNotes>
                        <releaseNotes></releaseNotes>
                        <language>en-US</language>
                      </metadata>
                    </package>");
        }

        private static Stream CreateTestPackageStreamWithDuplicateEmptyAndNonEmptyMetadataElements()
        {
            return CreateTestPackageStream(@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                        <title>Package A</title>
                        <title />
                        <authors>ownera, ownerb</authors>
                        <authors></authors>
                        <owners>ownera, ownerb</owners>
                        <owners>ownerc, ownerd</owners>
                        <description>

                        </description>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <language>

                        de-DE                        

                        </language>
                        <foo>1</foo>
                        <foo>2</foo>
                        <releaseNotes></releaseNotes>
                        <releaseNotes></releaseNotes>
                      </metadata>
                    </package>");
        }

        private static Stream CreateTestPackageStreamWithPackageTypes(params string[] packageTypes)
        {
            var packageTypeElements = packageTypes
                .Select(x => $"<packageType name=\"{x}\" />")
                .ToList();

            return CreateTestPackageStream($@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                        <packageTypes>
                          {string.Join(string.Empty, packageTypeElements)}
                        </packageTypes>
                      </metadata>
                    </package>");
        }

        private static Stream CreateTestPackageStream(string nuspec)
        {
            var packageStream = new MemoryStream();
            using (var packageArchive = new ZipArchive(packageStream, ZipArchiveMode.Create, true))
            {
                var nuspecEntry = packageArchive.CreateEntry("TestPackage.nuspec", CompressionLevel.Fastest);
                using (var streamWriter = new StreamWriter(nuspecEntry.Open()))
                {
                    streamWriter.WriteLine(nuspec);
                }

                packageArchive.CreateEntry("content\\HelloWorld.cs", CompressionLevel.Fastest);
            }

            packageStream.Position = 0;

            return packageStream;
        }
    }
}
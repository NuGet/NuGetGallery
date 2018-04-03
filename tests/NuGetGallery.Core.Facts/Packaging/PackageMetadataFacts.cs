// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGetGallery.Packaging
{
    public class PackageMetadataFacts
    {
        [Theory]
        [InlineData("developmentDependency")]
        [InlineData("requireLicenseAcceptance")]
        [InlineData("serviceable")]
        public void RejectsInvalidBooleanValue(string metadataName)
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithMetadataElementName(metadataName);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(nuspec, strict: false));
            Assert.Equal($"The package manifest contains an invalid boolean value for metadata element: '{metadataName}'. The value should be 'true' or 'false'.", ex.Message);
        }

        [Theory]
        [InlineData("created")]
        [InlineData("dependencyGroups")]
        [InlineData("isPrerelease")]
        [InlineData("lastEdited")]
        [InlineData("listed")]
        [InlineData("packageEntries")]
        [InlineData("packageHash")]
        [InlineData("packageHashAlgorithm")]
        [InlineData("packageSize")]
        [InlineData("published")]
        [InlineData("supportedFrameworks")]
        [InlineData("verbatimVersion")]
        public void RejectsRestrictedElementNames(string metadataName)
        {
            // Arrange
            var packageStream = CreateTestPackageStreamWithMetadataElementName(metadataName);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act & Assert
            var ex = Assert.Throws<PackagingException>(() => PackageMetadata.FromNuspecReader(nuspec, strict: false));
            Assert.Equal($"The package manifest contains invalid metadata elements: '{metadataName}'", ex.Message);
        }

        [Fact]
        public void AllowsUnrestrictedButUnofficialElementNames()
        {
            // Arrange
            var name = "foo";
            var packageStream = CreateTestPackageStreamWithMetadataElementName(name);
            var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act
            var packageMetadata = PackageMetadata.FromNuspecReader(
                nuspec,
                strict: true);

            // Assert
            Assert.Equal("TestPackage", packageMetadata.Id);
            Assert.Equal(NuGetVersion.Parse("0.0.0.1"), packageMetadata.Version);
            Assert.Equal("some value", packageMetadata.GetValueFromMetadata(name));
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
                      </metadata>
                    </package>");
        }

        private static Stream CreateTestPackageStreamWithMetadataElementName(string metadataName)
        {
            return CreateTestPackageStream($@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                        <{metadataName}>some value</{metadataName}>
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
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Packaging;
using NuGet.Versioning;
using Xunit;

namespace NuGetGallery.Packaging
{
    public class PackageMetadataFacts
    {
        [Fact]
        public static void CanReadBasicMetadataProperties()
        {
            var packageStream = CreateTestPackageStream();
            var nupkg = new PackageReader(packageStream, leaveStreamOpen: false);
            var nuspec = nupkg.GetNuspecReader();

            // Act
            var packageMetadata = PackageMetadata.FromNuspecReader(nuspec);

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

        private static Stream CreateTestPackageStream()
        {
            var packageStream = new MemoryStream();
            using (var packageArchive = new ZipArchive(packageStream, ZipArchiveMode.Create, true))
            {
                var nuspecEntry = packageArchive.CreateEntry("TestPackage.nuspec", CompressionLevel.Fastest);
                using (var streamWriter = new StreamWriter(nuspecEntry.Open()))
                {
                    streamWriter.WriteLine(@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>TestPackage</id>
                        <version>0.0.0.1</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
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

                packageArchive.CreateEntry("content\\HelloWorld.cs", CompressionLevel.Fastest);
            }

            packageStream.Position = 0;

            return packageStream;
        }
    }
}
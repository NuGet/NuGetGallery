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
    public class NupkgRewriterFacts
    {
        [Fact]
        public static void CanRewriteTheNuspecInANupkg()
        {
            var packageStream = CreateTestPackageStream();

            // Act
            NupkgRewriter.RewriteNupkgManifest(packageStream,
                    new List<Action<ManifestEdit>>
                    {
                        metadata => { metadata.Authors = "Me and You"; },
                        metadata => { metadata.Tags = "Peas In A Pod"; },
                        metadata => { metadata.ReleaseNotes = "In perfect harmony"; }
                    });

            // Assert
            using (var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: false))
            {
                var nuspec = nupkg.GetNuspecReader();

                Assert.Equal("TestPackage", nuspec.GetId());
                Assert.Equal(NuGetVersion.Parse("0.0.0.1"), nuspec.GetVersion());
                Assert.Equal("Me and You", nuspec.GetMetadata().First(kvp => kvp.Key == "authors").Value);
                Assert.Equal("Peas In A Pod", nuspec.GetMetadata().First(kvp => kvp.Key == "tags").Value);
                Assert.Equal("In perfect harmony", nuspec.GetMetadata().First(kvp => kvp.Key == "releaseNotes").Value);
            }
        }

        [Fact]
        public static void RewritingTheNuSpecDoesNotMessUpTheNuspecStream()
        {
            var packageStream = CreateTestPackageStream();
            var manifestStreamLengthOriginal = GetManifestStreamLength(packageStream);

            var longValue = new String('x', 200);
            var shortValue = "y";

            // Act 1 - Make the stream bigger
            NupkgRewriter.RewriteNupkgManifest(packageStream,
                    new List<Action<ManifestEdit>>
                    {
                        metadata => { metadata.Description = longValue; },
                        metadata => { metadata.Summary = longValue; }
                    });

            // Assert 1
            var manifestStreamLength1 = GetManifestStreamLength(packageStream);
            Assert.True(manifestStreamLength1 > manifestStreamLengthOriginal);

            using (var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
            {
                var nuspec = nupkg.GetNuspecReader();

                Assert.Equal("TestPackage", nuspec.GetId());
                Assert.Equal(NuGetVersion.Parse("0.0.0.1"), nuspec.GetVersion());
                Assert.Equal(longValue, nuspec.GetMetadata().First(kvp => kvp.Key == "description").Value);
                Assert.Equal(longValue, nuspec.GetMetadata().First(kvp => kvp.Key == "summary").Value);
            }

            // Act 2 - Make the stream smaller
            NupkgRewriter.RewriteNupkgManifest(packageStream,
                    new List<Action<ManifestEdit>>
                    {
                        metadata => { metadata.Description = shortValue; },
                        metadata => { metadata.Summary = shortValue; }
                    });

            // Assert 2
            var manifestStreamLength2 = GetManifestStreamLength(packageStream);
            Assert.True(manifestStreamLength2 < manifestStreamLength1);

            using (var nupkg = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
            {
                var nuspec = nupkg.GetNuspecReader();

                Assert.Equal("TestPackage", nuspec.GetId());
                Assert.Equal(NuGetVersion.Parse("0.0.0.1"), nuspec.GetVersion());
                Assert.Equal(shortValue, nuspec.GetMetadata().First(kvp => kvp.Key == "description").Value);
                Assert.Equal(shortValue, nuspec.GetMetadata().First(kvp => kvp.Key == "summary").Value);
            }
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
                        <dependencies />
                      </metadata>
                    </package>");
                }

                packageArchive.CreateEntry("content\\HelloWorld.cs", CompressionLevel.Fastest);
            }

            packageStream.Position = 0;

            return packageStream;
        }
        
        private static long GetManifestStreamLength(Stream packageStream)
        {
            using (var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true))
            {
                var manifestEntry = archive.Entries.Single(
                    entry => entry.Name.IndexOf("/", StringComparison.OrdinalIgnoreCase) == -1
                             && entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

                return manifestEntry.Length;
            }
        }
    }
}

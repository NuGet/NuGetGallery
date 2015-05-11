// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using Moq;
using NuGet;
using NuGetGallery.Packaging;
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
                    new List<Action<ManifestMetadata>>
                    {
                        (metadata) => { metadata.Authors = "Me and You"; },
                        (metadata) => { metadata.Tags = "Peas In A Pod"; },
                    });

            // Assert
            using (var nupkg = new Nupkg(packageStream, leaveOpen: false))
            {
                Assert.Equal("TestPackage", nupkg.Metadata.Id);
                Assert.Equal(SemanticVersion.Parse("0.0.0.1"), nupkg.Metadata.Version);
                Assert.Equal("Me and You", string.Join(" ", nupkg.Metadata.Authors));
                Assert.Equal("Peas In A Pod", nupkg.Metadata.Tags);
            }
        }

        private static Stream CreateTestPackageStream()
        {
            var packageStream = new MemoryStream();
            var builder = new PackageBuilder
            {
                Id = "TestPackage",
                Version = SemanticVersion.Parse("0.0.0.1"),
                Description = "Trivial Description",
                Authors = { "AuthorsIsRequiredSayWhaat?" },
            };

            var file = new Mock<IPackageFile>();
            file.Setup(s => s.Path).Returns(@"content\HelloWorld.cs");
            file.Setup(s => s.GetStream()).Returns(Stream.Null);
            builder.Files.Add(file.Object);

            builder.Save(packageStream);
            return packageStream;
        }
    }
}

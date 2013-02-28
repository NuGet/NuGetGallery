using System;
using System.IO;
using Moq;
using NuGet;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class NupkgFacts
    {
        public class PositiveScenarios
        {
            [Fact]
            public void ExtractTheManifest()
            {
                var packageStream = CreateTestPackageStream();
                packageStream.Position = 0;

                // Act
                using (var nupkg = new Nupkg(packageStream, leaveOpen: false))
                {
                    Assert.Equal("TestPackage", nupkg.Metadata.Id);
                    Assert.Equal(SemanticVersion.Parse("0.0.0.1"), nupkg.Metadata.Version);
                    Assert.Equal("Trivial Description", nupkg.Metadata.Description);
                }
            }

            [Fact]
            public void ExtractThePartsList()
            {
                var packageStream = CreateTestPackageStream();
                packageStream.Position = 0;

                // Act
                using (var nupkg = new Nupkg(packageStream, leaveOpen: false))
                {
                    var parts = nupkg.Parts;
                    Assert.Contains("/[Content_Types].xml", parts);
                    Assert.Contains("/_rels/.rels", parts);
                    Assert.Contains("/TestPackage.nuspec", parts);
                    Assert.Contains("/content/HelloWorld.cs", parts);
                }
            }

            [Fact]
            public void ExtractTheFilesList()
            {
                var packageStream = CreateTestPackageStream();
                packageStream.Position = 0;

                // Act
                using (var nupkg = new Nupkg(packageStream, leaveOpen: false))
                {
                    var files = nupkg.GetFiles();
                    Assert.DoesNotContain("/_rels/.rels", files);
                    Assert.DoesNotContain("/[Content_Types].xml", files);
                    Assert.DoesNotContain("_rels/.rels", files);
                    Assert.DoesNotContain("[Content_Types].xml", files);
                    Assert.Contains("TestPackage.nuspec", files);
                    Assert.Contains("content/HelloWorld.cs", files);
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

        public class TheGetLogicalPartNameMethod
        {
            [Fact]
            public void UnderstandsInterleavedItems()
            {
                bool interleaved;
                string name = Nupkg.GetLogicalPartName("folder1/file1.xml/[0].piece", out interleaved).ToString();
                Assert.True(interleaved);
                Assert.True(name.IndexOf("folder1/file1.xml", StringComparison.Ordinal) > 0);
                Assert.False(name.IndexOf("[0].piece", StringComparison.Ordinal) > 0);
                Assert.False(name.EndsWith("/", StringComparison.Ordinal));
            }

            [Fact]
            public void UnderstandsNonInterleavedItems()
            {
                bool interleaved;
                string name = Nupkg.GetLogicalPartName("folder1/file1.xml", out interleaved).ToString();
                Assert.False(interleaved);
                Assert.True(name.IndexOf("folder1/file1.xml", StringComparison.Ordinal) > 0);
                Assert.False(name.EndsWith("/", StringComparison.Ordinal));
            }

            [Fact]
            public void PrependsSlash()
            {
                bool interleaved;
                string name = Nupkg.GetLogicalPartName("file1.xml", out interleaved).ToString();
                Assert.False(interleaved);
                Assert.True(name.StartsWith("/", StringComparison.Ordinal));
            }
        }

        public class IgnorablePieces
        {
            [Fact]
            public void PiecesWithTrailingSlashesAreIgnored()
            {
                bool interleaved;
                bool ignored = !Nupkg.IsValidPartName(Nupkg.GetLogicalPartName("foo.txt/[13].piece/", out interleaved).ToString());
                Assert.True(ignored);
            }
        }

        public class TheIgnoredPartMethod
        {
            [Theory]
            [InlineData("/_rels/.rels")]
            [InlineData("/TestPackage.nuspec")]
            [InlineData("/content/HelloWorld.cs")]
            [InlineData("/package/services/metadata/core-properties/1cd48675fa0b4f89aecfa1fd01738c81.psmdcp")]
            [InlineData("/[Content_Types].xml")]
            public void LegitPartNamesAreAccepted(string logicalPartName)
            {
                bool valid = Nupkg.IsValidPartName(logicalPartName);
                Assert.True(valid);
            }

            [Fact]
            public void EmptyPartNamesAreIgnored()
            {
                bool valid = Nupkg.IsValidPartName("/");
                Assert.False(valid);
            }

            [Fact]
            public void PartsWithTrailingSlashesAreIgnored()
            {
                bool valid = Nupkg.IsValidPartName("/content/");
                Assert.False(valid);
            }

            [Theory]
            [InlineData("//")]
            [InlineData("/Dir1//file.txt")]
            [InlineData("/Dir1/Dir2//file.txt")]
            [InlineData("/Dir1//Dir2/file.txt")]
            public void PartsWithEmptyISegmentsAreIgnored(string logicalPartName)
            {
                bool valid = Nupkg.IsValidPartName(logicalPartName);
                Assert.False(valid);
            }

            [Theory]
            [InlineData("/./")]
            [InlineData("/Dir./")]
            [InlineData("/File./")]
            [InlineData("/Dir1/File.")]
            public void PartsWithDotEndingSegmentsAreIgnored(string logicalPartName)
            {
                bool valid = Nupkg.IsValidPartName(logicalPartName);
                Assert.False(valid);
            }

            [Theory]
            [InlineData("/./")]
            [InlineData("/../")]
            [InlineData("/.../")]
            [InlineData("/Dir1/./File.txt")]
            [InlineData("/Dir1/../File.txt")]
            public void PartsWithDotOnlyISegmentsAreIgnored(string logicalPartName)
            {
                bool valid = Nupkg.IsValidPartName(logicalPartName);
                Assert.False(valid);
            }
        }

        public class TheCanonicalNameMethod
        {
            [Fact]
            public void RemovesLeadingSlashes()
            {
                var canon = Nupkg.CanonicalName("/foo.txt");
                Assert.Equal("foo.txt", canon);
            }

            [Fact]
            public void RemovesLeadingBackSlashes()
            {
                var canon = Nupkg.CanonicalName(@"\foo.txt");
                Assert.Equal("foo.txt", canon);
            }

            [Fact]
            public void ConvertsBackSlashesToSlashes()
            {
                var canon = Nupkg.CanonicalName(@"Dir1\Dir2\foo.txt");
                Assert.Equal("Dir1/Dir2/foo.txt", canon);
            }
        }
    }
}

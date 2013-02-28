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

            private Stream CreateTestPackageStream()
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
                bool ignored = Nupkg.IsInvalidPartName(Nupkg.GetLogicalPartName("foo.txt/[13].piece/", out interleaved).ToString());
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
                bool ignored = Nupkg.IsInvalidPartName(logicalPartName);
                Assert.False(ignored);
            }

            [Fact]
            public void EmptyPartNamesAreIgnored()
            {
                bool ignored = Nupkg.IsInvalidPartName("/");
                Assert.True(ignored);
            }

            [Fact]
            public void PartsWithTrailingSlashesAreIgnored()
            {
                bool ignored = Nupkg.IsInvalidPartName("/content/");
                Assert.True(ignored);
            }

            //[Fact]
            //public void PartsWithBackslashesAreAnError(string logicalPartName)
            //{
            //    Assert.Throws<InvalidDataException>(
            //        () => Nupkg.IsInvalidPartName("/content\file.txt"));
            //}

            //[Fact]
            //public void PartsWithPercentEncodedSlashesAreAnError(string logicalPartName)
            //{
            //    Assert.Throws<InvalidDataException>(
            //        () => Nupkg.IsInvalidPartName("/content%2Ffile.txt"));
            //}

            [Theory]
            [InlineData("//")]
            [InlineData("/Dir1//file.txt")]
            [InlineData("/Dir1/Dir2//file.txt")]
            [InlineData("/Dir1//Dir2/file.txt")]
            public void PartsWithEmptyISegmentsAreIgnored(string logicalPartName)
            {
                bool ignored = Nupkg.IsInvalidPartName(logicalPartName);
                Assert.True(ignored);
            }

            [Theory]
            [InlineData("/./")]
            [InlineData("/Dir./")]
            [InlineData("/File./")]
            [InlineData("/Dir1/File.")]
            public void PartsWithDotEndingSegmentsAreIgnored(string logicalPartName)
            {
                bool ignored = Nupkg.IsInvalidPartName(logicalPartName);
                Assert.True(ignored);
            }

            [Theory]
            [InlineData("/./")]
            [InlineData("/../")]
            [InlineData("/.../")]
            [InlineData("/Dir1/./File.txt")]
            [InlineData("/Dir1/../File.txt")]
            public void PartsWithDotOnlyISegmentsAreIgnored(string logicalPartName)
            {
                bool ignored = Nupkg.IsInvalidPartName(logicalPartName);
                Assert.True(ignored);
            }

            //[Fact]
            public void WeDontVerifyContentTypes()
            {
                // Not really a test, just a statement of policy.
                // We don't go and verify content types exist for the part, because never extract parts (except nuspec - known content type) so there's not much point. Invalid packages there are the end users problem.

                // This is despite the normative statement
                // "The package implementer shall not map a logical item name or complete sequence of logical item names sharing a common prefix to a part name if the logical item prefix has no corresponding content type.""
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

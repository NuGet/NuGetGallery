// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Moq;
using NuGet;
using Xunit;

namespace NuGetGallery.Packaging
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
                    Assert.DoesNotContain("\\_rels\\.rels", files);
                    Assert.DoesNotContain("/[Content_Types].xml", files);
                    Assert.DoesNotContain("\\[Content_Types].xml", files);
                    Assert.DoesNotContain("_rels/.rels", files);
                    Assert.DoesNotContain("_rels\\.rels", files);
                    Assert.DoesNotContain("[Content_Types].xml", files);
                    Assert.Contains("TestPackage.nuspec", files);
                    Assert.Contains("content\\HelloWorld.cs", files);
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

        public class TheGetSupportedFrameworksMethod
        {
            [Fact]
            public void ReturnsNothingForNoFilesAndNoFrameworkReferences()
            {
                // Arrange
                var fakeStream = CreateTestPackageStream();
                var fakeNupkg = new Nupkg(fakeStream, leaveOpen: false);

                // Act
                var fx = fakeNupkg.GetSupportedFrameworks();

                // Assert
                Assert.Empty(fx);
            }

            [Fact]
            public void ReturnsFrameworkAssemblyReferenceFrameworksIfNoFileFrameworks()
            {
                // Arrange
                var fakeStream = CreateTestPackageStream(b =>
                {
                    b.FrameworkReferences.Add(new FrameworkAssemblyReference("foo", new[] {
                        VersionUtility.ParseFrameworkName("net45"),
                        VersionUtility.ParseFrameworkName("sl-wp70")
                    }));
                });
                var fakeNupkg = new Nupkg(fakeStream, leaveOpen: false);

                // Act
                var fx = fakeNupkg.GetSupportedFrameworks();

                // Assert
                Assert.Equal(new[] {
                    new FrameworkName(".NETFramework, Version=4.5"),
                    new FrameworkName("Silverlight, Version=v0.0, Profile=wp70")
                }, fx.ToArray());
            }

            [Theory]
            [MemberData("AllTheFrameworks"/*!*/)]
            public void ReturnsFilePathFrameworksIfPresent(string directoryName, FrameworkName fxName)
            {
                // Arrange
                var fakeStream = CreateTestPackageStream(b =>
                {
                    b.Files.Add(CreateMockPackageFile(@"lib\" + directoryName + @"\file.dll"));
                });
                var fakeNupkg = new Nupkg(fakeStream, leaveOpen: false);

                // Act
                var fx = fakeNupkg.GetSupportedFrameworks();

                // Assert
                Assert.Equal(new[] {
                    fxName,
                }, fx.ToArray());
            }

            [Fact]
            public void ReturnsUnionOfFrameworkReferenceAndFileFrameworks()
            {
                // Arrange
                var fakeStream = CreateTestPackageStream(b =>
                {
                    b.Files.Add(CreateMockPackageFile(@"lib\net40\file.dll"));
                    b.Files.Add(CreateMockPackageFile(@"lib\win\file.dll"));
                    b.FrameworkReferences.Add(new FrameworkAssemblyReference("Windows", new[] { new FrameworkName("Windows, Version=0.0") }));
                    b.FrameworkReferences.Add(new FrameworkAssemblyReference("System.Net.Http", new[] { new FrameworkName(".NETFramework, Version=2.0") }));
                });
                var fakeNupkg = new Nupkg(fakeStream, leaveOpen: false);

                // Act
                var fx = fakeNupkg.GetSupportedFrameworks();

                // Assert
                Assert.Equal(new[] {
                    new FrameworkName("Windows, Version=0.0"),
                    new FrameworkName(".NETFramework, Version=2.0"),
                    new FrameworkName(".NETFramework, Version=4.0"),
                }, fx.ToArray());
            }

            public static IEnumerable<object[]> AllTheFrameworks
            {
                get
                {
                    yield return new object[] { "net10", new FrameworkName(".NETFramework, Version=1.0") };
                    yield return new object[] { "net11", new FrameworkName(".NETFramework, Version=1.1") };
                    yield return new object[] { "net20", new FrameworkName(".NETFramework, Version=2.0") };
                    yield return new object[] { "net30", new FrameworkName(".NETFramework, Version=3.0") };
                    yield return new object[] { "net35", new FrameworkName(".NETFramework, Version=3.5") };
                    yield return new object[] { "net40", new FrameworkName(".NETFramework, Version=4.0") };
                    yield return new object[] { "net45", new FrameworkName(".NETFramework, Version=4.5") };
                    yield return new object[] { "netmf40", new FrameworkName(".NETMicroFramework, Version=4.0") };
                    yield return new object[] { "netmf41", new FrameworkName(".NETMicroFramework, Version=4.1") };
                    yield return new object[] { "netmf42", new FrameworkName(".NETMicroFramework, Version=4.2") };
                    yield return new object[] { "netmf43", new FrameworkName(".NETMicroFramework, Version=4.3") };
                    yield return new object[] { "netmf44", new FrameworkName(".NETMicroFramework, Version=4.4") };
                    yield return new object[] { "sl10", new FrameworkName("Silverlight, Version=1.0") };
                    yield return new object[] { "sl20", new FrameworkName("Silverlight, Version=2.0") };
                    yield return new object[] { "sl30", new FrameworkName("Silverlight, Version=3.0") };
                    yield return new object[] { "sl40", new FrameworkName("Silverlight, Version=4.0") };
                    yield return new object[] { "sl-wp", new FrameworkName("Silverlight, Version=0.0, Profile=WindowsPhone") };
                    yield return new object[] { "sl-wp71", new FrameworkName("Silverlight, Version=0.0, Profile=WindowsPhone71") };
                    yield return new object[] { "win", new FrameworkName("Windows, Version=0.0") };
                    yield return new object[] { "winrt", new FrameworkName(".NETCore, Version=0.0") };
                    yield return new object[] { "winrt80", new FrameworkName(".NETCore, Version=8.0") };
                    yield return new object[] { "win80", new FrameworkName("Windows, Version=8.0") };
                    yield return new object[] { "win81", new FrameworkName("Windows, Version=8.1") }; // Just making stuff up ;)
                    yield return new object[] { "wp", new FrameworkName("WindowsPhone, Version=0.0") };
                    yield return new object[] { "wp70", new FrameworkName("WindowsPhone, Version=7.0") };
                    yield return new object[] { "wp71", new FrameworkName("WindowsPhone, Version=7.1") };
                    yield return new object[] { "wp80", new FrameworkName("WindowsPhone, Version=8.0") };
                    yield return new object[] { "MonoAndroid", new FrameworkName("MonoAndroid, Version=0.0") };
                    yield return new object[] { "MonoAndroid30", new FrameworkName("MonoAndroid, Version=3.0") };
                    yield return new object[] { "MonoAndroid45", new FrameworkName("MonoAndroid, Version=4.5") };
                    yield return new object[] { "MonoTouch", new FrameworkName("MonoTouch, Version=0.0") };
                    yield return new object[] { "MonoTouch30", new FrameworkName("MonoTouch, Version=3.0") };
                    yield return new object[] { "MonoTouch45", new FrameworkName("MonoTouch, Version=4.5") };
                    yield return new object[] { "MonoMac", new FrameworkName("MonoMac, Version=0.0") };
                    yield return new object[] { "MonoMac30", new FrameworkName("MonoMac, Version=3.0") };
                    yield return new object[] { "MonoMac45", new FrameworkName("MonoMac, Version=4.5") };
                    yield return new object[] { "native", new FrameworkName("native, Version=0.0") };
                    yield return new object[] { "native30", new FrameworkName("native, Version=3.0") };
                    yield return new object[] { "native45", new FrameworkName("native, Version=4.5") };
                    yield return new object[] { "portable-net10", new FrameworkName(".NETPortable, Version=0.0, Profile=net10") };
                    yield return new object[] { "portable-net10+net40", new FrameworkName(".NETPortable, Version=0.0, Profile=net10+net40") };
                    yield return new object[] { "portable-net10+wp71+win81", new FrameworkName(".NETPortable, Version=0.0, Profile=net10+wp71+win81") };
                    yield return new object[] { "portable-native+win", new FrameworkName(".NETPortable, Version=0.0, Profile=native+win") };
                    yield return new object[] { "portable-win81+wp80", new FrameworkName(".NETPortable, Version=0.0, Profile=win81+wp80") };
                    yield return new object[] { "portable-native30+monomac+monotouch+monoandroid", new FrameworkName(".NETPortable, Version=0.0, Profile=native30+monomac+monotouch+monoandroid") };
                }
            }

            private static Stream CreateTestPackageStream(Action<PackageBuilder> additionalConfig = null)
            {
                var packageStream = new MemoryStream();
                var builder = new PackageBuilder
                {
                    Id = "TestPackage",
                    Version = SemanticVersion.Parse("0.0.0.1"),
                    Description = "Trivial Description",
                    Authors = { "AuthorsIsRequiredSayWhaat?" },
                };

                if (additionalConfig != null)
                {
                    additionalConfig(builder);
                }

                // Make the package buildable by adding a dependency if the additional config didn't add any files/deps
                if (builder.Files.Count == 0 && !builder.DependencySets.Any(s => s.Dependencies.Any()))
                {
                    builder.DependencySets.Add(new PackageDependencySet(null, new[] { new NuGet.PackageDependency("dummy") }));
                }

                builder.Save(packageStream);
                return packageStream;
            }

            private static IPackageFile CreateMockPackageFile(string path)
            {
                var mock = new Mock<IPackageFile>();
                mock.Setup(f => f.Path).Returns(path);
                mock.Setup(s => s.GetStream()).Returns(Stream.Null);
                return mock.Object;
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace NuGetGallery.Packaging
{
    public class ManifestValidatorFacts
    {
        private const string NuSpecIdNotPresent = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id></id>
                        <version>1.0.1-alpha</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecIdTooLong = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789A</id>
                        <version>1.0.1-alpha</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecIdInvalid = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>not a valid id</id>
                        <version>1.0.1-alpha</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecVersionInvalid1 = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>valid</id>
                        <version>1 2 3</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecVersionInvalid2 = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>valid</id>
                        <version>2</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecSemVer200 = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>valid</id>
                        <version>2.0.0+123</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecDependencyVersionPlaceholder = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>valid</id>
                        <version>2.0.0</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies>
                            <group targetFramework=""net40"">
                              <dependency id=""Dep"" version=""{0}"" />
                            </group>
                        </dependencies>
                      </metadata>
                    </package>";

        private const string NuSpecPlaceholderVersion = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>valid</id>
                        <version>{0}</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecIconUrlInvalid = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>valid</id>
                        <version>1.0.1-alpha</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <iconUrl>http://a b c d</iconUrl>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecProjectUrlInvalid = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>valid</id>
                        <version>1.0.1-alpha</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <projectUrl>http://a b c d</projectUrl>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecLicenseUrlInvalid = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>valid</id>
                        <version>1.0.1-alpha</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <licenseUrl>http://a b c d</licenseUrl>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecUrlNotHttpOrHttps = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>valid</id>
                        <version>1.0.1-alpha</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <licenseUrl>javascript:alert('test');</licenseUrl>
                        <dependencies />
                      </metadata>
                    </package>";

        private const string NuSpecDependencySetContainsInvalidId = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""a b c"" version=""1.0"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                        </group>
                        <group targetFramework=""wp8"">
                          <dependency id=""jQuery"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        private const string NuSpecDependencySetContainsEmptyTargetFramework = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <group targetFramework="""">
                          <dependency id=""a.b.c"" version=""1.0-alpha"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        private const string NuSpecDependencySetContainsInvalidVersionRange = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <dependency id=""a.b.c"" version=""{0}"" />
                    </dependencies>
                  </metadata>
                </package>";

        private const string NuSpecFrameworkAssemblyReferenceContainsEmptyTargetFramework = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <frameworkAssemblies>
                      <frameworkAssembly assemblyName=""System.ServiceModel"" targetFramework="""" />
                    </frameworkAssemblies>
                  </metadata>
                </package>";

        private const string NuSpecFrameworkAssemblyReferenceContainsUnsupportedTargetFramework = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <frameworkAssemblies>
                      <frameworkAssembly assemblyName=""System.ServiceModel"" targetFramework=""Unsupported0.0"" />
                    </frameworkAssemblies>
                  </metadata>
                </package>";

        private const string NuSpecFrameworkAssemblyReferenceContainsDuplicateDependency = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""SomeDependency"" version=""1.0.0-alpha1"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                          <dependency id=""SomeDependency"" version=""1.0.2"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        private const string NuSpecDependenciesContainsUnsupportedTargetFramework = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""SomeDependency"" version=""1.0.0-alpha1"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                        </group>
                        <group targetFramework=""Unsupported0.0"">
                          <dependency id=""SomeDependency"" version=""1.0.0-alpha1"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        private const string NuspecDependencyWithVersionRangeFormat = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <description>package A description.</description>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""SomeDependency"" version=""{0}"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        [Fact]
        public void ReturnsErrorIfIdNotPresent()
        {
            var nuspecStream = CreateNuspecStream(NuSpecIdNotPresent);

            Assert.Equal(new[] { CoreStrings.Manifest_MissingId }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfIdTooLong()
        {
            var nuspecStream = CreateNuspecStream(NuSpecIdTooLong);

            Assert.Equal(new[] { CoreStrings.Manifest_IdTooLong }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfIdInvalid()
        {
            var nuspecStream = CreateNuspecStream(NuSpecIdInvalid);

            Assert.Equal(new[] { String.Format(CoreStrings.Manifest_InvalidId, "not a valid id") }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfIconUrlInvalid()
        {
            var nuspecStream = CreateNuspecStream(NuSpecIconUrlInvalid);

            Assert.Equal(new[] { CoreStrings.Manifest_InvalidUrl }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfProjectUrlInvalid()
        {
            var nuspecStream = CreateNuspecStream(NuSpecProjectUrlInvalid);

            Assert.Equal(new[] { CoreStrings.Manifest_InvalidUrl }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfLicenseUrlInvalid()
        {
            var nuspecStream = CreateNuspecStream(NuSpecLicenseUrlInvalid);

            Assert.Equal(new[] { CoreStrings.Manifest_InvalidUrl }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfUrlNotHttpOrHttps()
        {
            var nuspecStream = CreateNuspecStream(NuSpecUrlNotHttpOrHttps);

            Assert.Equal(new[] { CoreStrings.Manifest_InvalidUrl }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfVersionInvalid1()
        {
            var nuspecStream = CreateNuspecStream(NuSpecVersionInvalid1);

            Assert.Equal(new[] { "The version string '1 2 3' is invalid." }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfVersionInvalid2()
        {
            var nuspecStream = CreateNuspecStream(NuSpecVersionInvalid2);

            Assert.Equal(new[] { "The version string '2' is invalid." }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsNoErrorIfVersionIsSemVer200()
        {
            var nuspecStream = CreateNuspecStream(NuSpecSemVer200);

            Assert.Empty(GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsNoErrorIfDependencyVersionIsSemVer200WithoutMetadataPart()
        {
            const string version = "1.0.0-beta.1";
            var nuspecStream = CreateNuspecStream(string.Format(NuSpecDependencyVersionPlaceholder, version));
            
            Assert.Empty(GetErrors(nuspecStream));
        }

        [Theory]
        [InlineData("1.0.0-10")]
        [InlineData("1.0.0--")]
        public void ReturnsErrorIfNonSemVer2VersionIsNotCompliantWith2XClients(string version)
        {
            var nuspecStream = CreateNuspecStream(string.Format(NuSpecPlaceholderVersion, version));

            Assert.Equal(new[] {String.Format(CoreStrings.Manifest_InvalidVersion, version)}, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfDependencyVersionIsSemVer200WithMetadataPart()
        {
            const string version = "3.0.0-beta+12";
            var nuspecStream = CreateNuspecStream(string.Format(NuSpecDependencyVersionPlaceholder, version));
            
            Assert.Equal(new[] { String.Format(CoreStrings.Manifest_InvalidDependencyVersion, version) }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfFrameworkAssemblyReferenceContainsUnsupportedTargetFramework()
        {
            var nuspecStream = CreateNuspecStream(NuSpecFrameworkAssemblyReferenceContainsUnsupportedTargetFramework);

            Assert.Equal(new[] { String.Format(CoreStrings.Manifest_TargetFrameworkNotSupported, "Unsupported,Version=v0.0") }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfDependencySetContainsInvalidId()
        {
            var nuspecStream = CreateNuspecStream(NuSpecDependencySetContainsInvalidId);
            
            Assert.Equal(new[] {"Invalid package version for a dependency with id 'jQuery' in package 'packageA.1.0.1-alpha': ''"}, GetErrors(nuspecStream));
        }
        
        [Fact]
        public void NoErrorIfDependencySetContainsEmptyTargetFramework()
        {
            var nuspecStream = CreateNuspecStream(NuSpecDependencySetContainsEmptyTargetFramework);
            
            Assert.Empty(GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfDependencySetContainsUnsupportedTargetFramework()
        {
            var nuspecStream = CreateNuspecStream(NuSpecDependenciesContainsUnsupportedTargetFramework);

            Assert.Equal(new[] { String.Format(CoreStrings.Manifest_TargetFrameworkNotSupported, "Unsupported,Version=v0.0") }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfDependencySetContainsDuplicateDependency()
        {
            var nuspecStream = CreateNuspecStream(NuSpecFrameworkAssemblyReferenceContainsDuplicateDependency);

            Assert.Equal(new[] { String.Format(CoreStrings.Manifest_DuplicateDependency, "net40", "SomeDependency") }, GetErrors(nuspecStream));
        }

        [Theory]
        [InlineData("*")]
        [InlineData("1.*")]
        [InlineData("1.0.*")]
        [InlineData("1.0.0.*")]
        [InlineData("1.0.0-beta*")]
        [InlineData("1.0.0-beta-*")]
        [InlineData("1.0.0-beta.*")]
        public void ReturnsErrorIfDependencyHasInvalidVersionRange(string versionRange)
        {
            var nuspec = string.Format(NuspecDependencyWithVersionRangeFormat, versionRange);
            var nuspecStream = CreateNuspecStream(nuspec);

            Assert.Equal(new[] { string.Format(CoreStrings.Manifest_InvalidDependencyVersionRange, versionRange) }, GetErrors(nuspecStream));
        }

        [Fact]
        public void NoErrorIfFrameworkAssemblyReferenceContainsEmptyTargetFramework()
        {
            var nuspecStream = CreateNuspecStream(NuSpecFrameworkAssemblyReferenceContainsEmptyTargetFramework);
            
            Assert.Empty(GetErrors(nuspecStream));
        }

        [Theory]
        [InlineData("$version$")]
        [InlineData("[15.106.0.preview]")]
        [InlineData("0.0.0-~4")]
        [InlineData("(2.0.0, 1.0.0(")]
        public void ReturnsErrorIfDependencyVersionRangeInvalid(string versionRange)
        {
            // Arrange
            var nuspec = string.Format(NuSpecDependencySetContainsInvalidVersionRange, versionRange);
            var nuspecStream = CreateNuspecStream(nuspec);

            // Act
            var errors = GetErrors(nuspecStream);

            // Assert
            Assert.Equal(new[] { $"Invalid package version for a dependency with id 'a.b.c' in package 'packageA.1.0.1-alpha': '{versionRange}'" }, errors);
        }

        [Fact]
        public void ReturnsPackageMetadataForValidNuspec()
        {
            // Arrange
            var nuspecStream = CreateNuspecStream(NuSpecSemVer200);

            // Act
            ManifestValidator.Validate(nuspecStream, out var reader, out var packageMetadata);

            // Assert
            Assert.NotNull(packageMetadata);
        }

        private static string[] GetErrors(Stream nuspecStream)
        {
            return ManifestValidator
                .Validate(nuspecStream, out var reader, out var metadata)
                .Select(r => r.ErrorMessage)
                .ToArray();
        }

        private static Stream CreateNuspecStream(string nuspec)
        {
            byte[] byteArray = Encoding.ASCII.GetBytes(nuspec);
            return new MemoryStream(byteArray);
        }
    }
}

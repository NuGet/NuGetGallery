// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NuGet.Packaging;
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

        private const string NuSpecVersionInvalid = @"<?xml version=""1.0""?>
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

        private const string NuSpecDependencySetContainsInvalidTargetFramework = @"<?xml version=""1.0""?>
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
                        <group targetFramework=""net40-client-full-awesome-unicorns"">
                          <dependency id=""a.b.c"" version=""1.0-alpha"" />
                        </group>
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

        private const string NuSpecFrameworkAssemblyReferenceContainsInvalidTargetFramework = @"<?xml version=""1.0""?>
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
                      <frameworkAssembly assemblyName=""System.ServiceModel"" targetFramework=""net40-client-full-awesome-unicorns"" />
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

        [Fact]
        public void ReturnsErrorIfIdNotPresent()
        {
            var nuspecStream = CreateNuspecStream(NuSpecIdNotPresent);

            Assert.Equal(new[] { Strings.Manifest_MissingId }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfIdTooLong()
        {
            var nuspecStream = CreateNuspecStream(NuSpecIdTooLong);

            Assert.Equal(new[] { Strings.Manifest_IdTooLong }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfIdInvalid()
        {
            var nuspecStream = CreateNuspecStream(NuSpecIdInvalid);

            Assert.Equal(new[] { String.Format(Strings.Manifest_InvalidId, "not a valid id") }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfIconUrlInvalid()
        {
            var nuspecStream = CreateNuspecStream(NuSpecIconUrlInvalid);

            Assert.Equal(new[] { Strings.Manifest_InvalidUrl }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfProjectUrlInvalid()
        {
            var nuspecStream = CreateNuspecStream(NuSpecProjectUrlInvalid);

            Assert.Equal(new[] { Strings.Manifest_InvalidUrl }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfLicenseUrlInvalid()
        {
            var nuspecStream = CreateNuspecStream(NuSpecLicenseUrlInvalid);

            Assert.Equal(new[] { Strings.Manifest_InvalidUrl }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfVersionInvalid()
        {
            var nuspecStream = CreateNuspecStream(NuSpecVersionInvalid);

            Assert.Equal(new[] { String.Format(Strings.Manifest_InvalidVersion, "1 2 3") }, GetErrors(nuspecStream));
        }

        [Fact]
        public void ReturnsErrorIfDependencySetContainsInvalidId()
        {
            var nuspecStream = CreateNuspecStream(NuSpecDependencySetContainsInvalidId);

            Assert.Equal(new[] { String.Format(Strings.Manifest_InvalidDependency, "a b c", "1.0") }, GetErrors(nuspecStream));
        }
        
        [Fact]
        public void NoErrorIfDependencySetContainsEmptyTargetFramework()
        {
            var nuspecStream = CreateNuspecStream(NuSpecDependencySetContainsEmptyTargetFramework);
            
            Assert.Equal(GetErrors(nuspecStream).Length, 0);
        }
        
        [Fact]
        public void ReturnsErrorIfDependencySetContainsDuplicateDependency()
        {
            var nuspecStream = CreateNuspecStream(NuSpecFrameworkAssemblyReferenceContainsDuplicateDependency);

            Assert.Equal(new[] { String.Format(Strings.Manifest_DuplicateDependency, "net40", "SomeDependency") }, GetErrors(nuspecStream));
        }

        [Fact]
        public void NoErrorIfFrameworkAssemblyReferenceContainsEmptyTargetFramework()
        {
            var nuspecStream = CreateNuspecStream(NuSpecFrameworkAssemblyReferenceContainsEmptyTargetFramework);
            
            Assert.Equal(GetErrors(nuspecStream).Length, 0);
        }

        private string[] GetErrors(Stream nuspecStream)
        {
            NuspecReader reader;

            return ManifestValidator
                .Validate(nuspecStream, out reader)
                .Select(r => r.ErrorMessage)
                .ToArray();
        }

        private Stream CreateNuspecStream(string nuspec)
        {
            byte[] byteArray = Encoding.ASCII.GetBytes(nuspec);
            return new MemoryStream(byteArray);
        }
    }
}

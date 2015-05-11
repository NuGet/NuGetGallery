// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;
using Xunit;

namespace NuGetGallery.Packaging
{
    public class ManifestValidatorFacts
    {
        [Fact]
        public void ReturnsErrorIfIdNotPresent()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = null,
                    Version = "1.0.0"
                }
            };
            Assert.Equal(new[] { Strings.Manifest_MissingId }, GetErrors(m));
        }

        [Fact]
        public void ReturnsErrorIfIdTooLong()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = new String('a', 101),
                    Version = "1.0.0"
                }
            };
            Assert.Equal(new[] { Strings.Manifest_IdTooLong }, GetErrors(m));
        }

        [Fact]
        public void ReturnsErrorIfIdInvalid()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "not a valid id",
                    Version = "1.0.0"
                }
            };
            Assert.Equal(new[] { String.Format(Strings.Manifest_InvalidId, "not a valid id") }, GetErrors(m));
        }

        [Fact]
        public void ReturnsErrorIfIconUrlInvalid()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1.0.0",
                    IconUrl = "http://a b c d"
                }
            };
            Assert.Equal(new[] { Strings.Manifest_InvalidUrl }, GetErrors(m));
        }

        [Fact]
        public void ReturnsErrorIfProjectUrlInvalid()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1.0.0",
                    ProjectUrl = "http://a b c d"
                }
            };
            Assert.Equal(new[] { Strings.Manifest_InvalidUrl }, GetErrors(m));
        }

        [Fact]
        public void ReturnsErrorIfLicenseUrlInvalid()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1.0.0",
                    LicenseUrl = "http://a b c d"
                }
            };
            Assert.Equal(new[] { Strings.Manifest_InvalidUrl }, GetErrors(m));
        }

        [Fact]
        public void ReturnsErrorIfVersionInvalid()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1 2 3",
                }
            };
            Assert.Equal(new[] { String.Format(Strings.Manifest_InvalidVersion, "1 2 3") }, GetErrors(m));
        }

        [Fact]
        public void ReturnsErrorIfDependencySetContainsInvalidId()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1.0.0",
                    DependencySets = new List<ManifestDependencySet>()
                    {
                        new ManifestDependencySet() {
                            TargetFramework = "net40",
                            Dependencies = new List<ManifestDependency>() {
                                new ManifestDependency() {
                                    Id = "a b c",
                                    Version = "1.0"
                                }
                            }
                        }
                    }
                }
            };
            Assert.Equal(new[] { String.Format(Strings.Manifest_InvalidDependency, "a b c", "1.0") }, GetErrors(m));
        }

        [Fact]
        public void ReturnsErrorIfDependencySetContainsInvalidVersion()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1.0.0",
                    DependencySets = new List<ManifestDependencySet>()
                    {
                        new ManifestDependencySet() {
                            TargetFramework = "net40",
                            Dependencies = new List<ManifestDependency>() {
                                new ManifestDependency() {
                                    Id = "a.b.c",
                                    Version = "1.0 alpha"
                                }
                            }
                        }
                    }
                }
            };
            Assert.Equal(new[] { String.Format(Strings.Manifest_InvalidDependency, "a.b.c", "1.0 alpha") }, GetErrors(m));
        }

        [Fact]
        public void NoErrorIfDependencySetContainsEmptyTargetFramework()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1.0.0",
                    DependencySets = new List<ManifestDependencySet>()
                    {
                        new ManifestDependencySet() {
                            TargetFramework = "",
                            Dependencies = new List<ManifestDependency>() {
                                new ManifestDependency() {
                                    Id = "a.b.c",
                                    Version = "1.0-alpha"
                                }
                            }
                        }
                    }
                }
            };
            Assert.Equal(GetErrors(m).Length, 0);
        }

        [Fact]
        public void ReturnsErrorIfDependencySetContainsInvalidTargetFramework()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1.0.0",
                    DependencySets = new List<ManifestDependencySet>()
                    {
                        new ManifestDependencySet() {
                            TargetFramework = "net40-client-full-awesome-unicorns",
                            Dependencies = new List<ManifestDependency>() {
                                new ManifestDependency() {
                                    Id = "a.b.c",
                                    Version = "1.0-alpha"
                                }
                            }
                        }
                    }
                }
            };
            Assert.Equal(new[] { String.Format(Strings.Manifest_InvalidTargetFramework, "net40-client-full-awesome-unicorns") }, GetErrors(m));
        }

        [Fact]
        public void NoErrorIfFrameworkAssemblyReferenceContainsEmptyTargetFramework()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1.0.0",
                    FrameworkAssemblies = new List<ManifestFrameworkAssembly>()
                    {
                        new ManifestFrameworkAssembly() {
                            TargetFramework = "",
                            AssemblyName = "System.Awesome"
                        }
                    }
                }
            };
            Assert.Equal(GetErrors(m).Length, 0);
        }

        [Fact]
        public void ReturnsErrorIfFrameworkAssemblyReferenceContainsInvalidTargetFramework()
        {
            Manifest m = new Manifest()
            {
                Metadata = new ManifestMetadata()
                {
                    Id = "valid",
                    Version = "1.0.0",
                    FrameworkAssemblies = new List<ManifestFrameworkAssembly>()
                    {
                        new ManifestFrameworkAssembly() {
                            TargetFramework = "net40-client-full-awesome-unicorns",
                            AssemblyName = "System.Awesome"
                        }
                    }
                }
            };
            Assert.Equal(new[] { String.Format(Strings.Manifest_InvalidTargetFramework, "net40-client-full-awesome-unicorns") }, GetErrors(m));
        }

        private string[] GetErrors(Manifest m)
        {
            return ManifestValidator
                .Validate(m)
                .Select(r => r.ErrorMessage)
                .ToArray();
        }
    }
}

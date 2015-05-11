// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Linq;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class DisplayPackageViewModelFacts
    {
        [Fact]
        public void TheCtorSortsPackageVersionsProperly()
        {
            var package = new Package
                {
                    Dependencies = Enumerable.Empty<PackageDependency>().ToList(),
                    PackageRegistration = new PackageRegistration
                        {
                            Owners = Enumerable.Empty<User>().ToList(),
                        }
                };

            package.PackageRegistration.Packages = new[]
                {
                    new Package { Version = "1.0.0-alpha2", PackageRegistration = package.PackageRegistration }
                    ,
                    new Package { Version = "1.0.0", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.0-alpha", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.0-beta", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.2-beta", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.2", PackageRegistration = package.PackageRegistration },
                    new Package { Version = "1.0.10", PackageRegistration = package.PackageRegistration }
                };

            var packageVersions = new DisplayPackageViewModel(package).PackageVersions.ToList();

            // Descending
            Assert.Equal("1.0.0-alpha", packageVersions[6].Version);
            Assert.Equal("1.0.0-alpha2", packageVersions[5].Version);
            Assert.Equal("1.0.0-beta", packageVersions[4].Version);
            Assert.Equal("1.0.0", packageVersions[3].Version);
            Assert.Equal("1.0.2-beta", packageVersions[2].Version);
            Assert.Equal("1.0.2", packageVersions[1].Version);
            Assert.Equal("1.0.10", packageVersions[0].Version);
        }
    }
}
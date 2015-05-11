// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class DependencySetsViewModelFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void GivenAListOfDependenciesItShouldGroupByTargetFrameworkName()
            {
                // Arrange
                var deps = new[] {
                    new PackageDependency() { TargetFramework = null },
                    new PackageDependency() { TargetFramework = "portable-net45+win8" },
                    new PackageDependency() { TargetFramework = "portable-net40+sl40+win8+wp71", Id = "Microsoft.Net.Http", VersionSpec = "[2.1,3.0)" },
                };

                // Act
                var vm = new DependencySetsViewModel(deps);

                // Assert
                Assert.Equal(3, vm.DependencySets.Count);
                Assert.Null(vm.DependencySets["All Frameworks"].Single());
                Assert.Null(vm.DependencySets["Portable Class Library (.NETFramework 4.5, Windows 8.0)"].Single());

                var actual = vm.DependencySets["Portable Class Library (.NETFramework 4.0, Silverlight 4.0, Windows 8.0, WindowsPhone 7.1)"].ToArray();
                Assert.Equal(1, actual.Length);
                Assert.Equal("Microsoft.Net.Http", actual[0].Id);
                Assert.Equal("(≥ 2.1 && < 3.0)", actual[0].VersionSpec);
            }
        }
    }
}

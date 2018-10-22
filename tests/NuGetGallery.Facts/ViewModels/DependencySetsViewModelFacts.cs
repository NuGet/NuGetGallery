// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Services.Entities;
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
                var dependencies = new[] {
                    new PackageDependency { TargetFramework = null },
                    new PackageDependency { TargetFramework = "portable-net45+win8" },
                    new PackageDependency { TargetFramework = "portable-net40+sl40+win8+wp71", Id = "Microsoft.Net.Http", VersionSpec = "[2.1,3.0)" },
                };

                // Act
                var vm = new DependencySetsViewModel(dependencies);

                // Assert
                Assert.Equal(3, vm.DependencySets.Count);
                Assert.Null(vm.DependencySets["All Frameworks"].Single());
                Assert.Null(vm.DependencySets["Portable Class Library (.NETFramework 4.5, Windows 8.0)"].Single());

                var actual = vm.DependencySets["Portable Class Library (.NETFramework 4.0, Silverlight 4.0, Windows 8.0, WindowsPhone 7.1)"].ToArray();
                Assert.Single(actual);
                Assert.Equal("Microsoft.Net.Http", actual[0].Id);
                Assert.Equal("(>= 2.1.0 && < 3.0.0)", actual[0].VersionSpec);
            }

            [Fact]
            public void GivenAListOfDependenciesTargetFrameworksWillBeOrdered()
            {
                // Arrange
                var dependencies = new[] {
                    new PackageDependency { TargetFramework = "sl50" },
                    new PackageDependency { TargetFramework = "monoandroid23" },
                    new PackageDependency { TargetFramework = "net45" },
                    new PackageDependency { TargetFramework = "sl40" },
                    new PackageDependency { TargetFramework = "net462"},
                    new PackageDependency { TargetFramework = "netstandard1.5" },
                    new PackageDependency { TargetFramework = "netstandard1.3" }
                };

                // Act
                var viewModel = new DependencySetsViewModel(dependencies);

                // Assert
                Assert.Equal(7, viewModel.DependencySets.Count);

                var dependencySetsList = viewModel.DependencySets.Keys.ToList();

                Assert.Equal(".NETFramework 4.5", dependencySetsList[0]);
                Assert.Equal(".NETFramework 4.6.2", dependencySetsList[1]);
                Assert.Equal(".NETStandard 1.3", dependencySetsList[2]);
                Assert.Equal(".NETStandard 1.5", dependencySetsList[3]);
                Assert.Equal("MonoAndroid 2.3", dependencySetsList[4]);
                Assert.Equal("Silverlight 4.0", dependencySetsList[5]);
                Assert.Equal("Silverlight 5.0", dependencySetsList[6]);
            }

            [Fact]
            public void GivenAListOfDependenciesPackageIdsWillBeOrdered()
            {
                // Arrange
                var dependencies = new[] {
                    new PackageDependency { TargetFramework = null, Id = "cde" },
                    new PackageDependency { TargetFramework = null, Id = "abc" },
                    new PackageDependency { TargetFramework = null, Id = "bcd" },
                    new PackageDependency { TargetFramework = null, Id = "def" }
                };

                // Act
                var viewModel = new DependencySetsViewModel(dependencies);

                // Assert
                Assert.Equal(1, viewModel.DependencySets.Count);
                Assert.Equal(4, viewModel.DependencySets.First().Value.Count());

                var dependencyViewModels = viewModel.DependencySets.First().Value.ToList();

                Assert.Equal("abc", dependencyViewModels[0].Id);
                Assert.Equal("bcd", dependencyViewModels[1].Id);
                Assert.Equal("cde", dependencyViewModels[2].Id);
                Assert.Equal("def", dependencyViewModels[3].Id);
            }
        }
    }
}

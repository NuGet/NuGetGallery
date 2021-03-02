// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.Extensions
{
    public class CakeBuildManagerExtensionsFacts
    {
        public class TheMethodIsCakeExtension
        {
            public class GivenAPackageWithKnownCakeTags
            {
                [Theory]
                [ClassData(typeof(PackageTagsKnownToBeCakeExtensions))]
                public void ReturnsFalse(string[] tags)
                {
                    var model = new DisplayPackageViewModel
                    {
                        Tags = tags,
                    };

                    var actual = model.IsCakeExtension();

                    Assert.True(actual);
                }
            }

            public class GivenAPackageWithoutAnyKnownCakeTags
            {
                [Theory]
                [ClassData(typeof(PackageTagsNotKnownToBeCakeExtensions))]
                public void ReturnsFalse(string[] tags)
                {
                    var model = new DisplayPackageViewModel
                    {
                        Tags = tags,
                    };

                    var actual = model.IsCakeExtension();

                    Assert.False(actual);
                }
            }
        }

        public class TheMethodGetCakeInstallPackageCommand
        {
            public class GivenAPackageWithTheCakeAddinTag
            {
                [Fact]
                public void ReturnsAnAddinDirective()
                {
                    var model = new DisplayPackageViewModel
                    {
                        Id = "Cake.7zip",
                        Version = "1.0.0",
                        Tags = new[] { "cake-addin" },
                    };

                    var actual = model.GetCakeInstallPackageCommand();

                    Assert.Equal("#addin nuget:?package=Cake.7zip&version=1.0.0", actual);
                }

                [Fact]
                public void ReturnsAnAddinDirectiveWithPrerelease()
                {
                    var model = new DisplayPackageViewModel
                    {
                        Id = "Cake.7zip",
                        Version = "1.0.0",
                        Tags = new[] { "cake-addin" },
                        Prerelease = true,
                    };

                    var actual = model.GetCakeInstallPackageCommand();

                    Assert.Equal("#addin nuget:?package=Cake.7zip&version=1.0.0&prerelease", actual);
                }
            }

            public class GivenAPackageWithTheCakeModuleTag
            {
                [Fact]
                public void ReturnsAModuleDirective()
                {
                    var model = new DisplayPackageViewModel
                    {
                        Id = "Cake.BuildSystems.Module",
                        Version = "1.0.0",
                        Tags = new[] {"cake-module"},
                    };

                    var actual = model.GetCakeInstallPackageCommand();

                    Assert.Equal("#module nuget:?package=Cake.BuildSystems.Module&version=1.0.0", actual);
                }

                [Fact]
                public void ReturnsAModuleDirectiveWithPrerelease()
                {
                    var model = new DisplayPackageViewModel
                    {
                        Id = "Cake.BuildSystems.Module",
                        Version = "1.0.0",
                        Tags = new[] { "cake-module" },
                        Prerelease = true,
                    };

                    var actual = model.GetCakeInstallPackageCommand();

                    Assert.Equal("#module nuget:?package=Cake.BuildSystems.Module&version=1.0.0&prerelease", actual);
                }
            }

            public class GivenAPackageWithTheCakeRecipeTag
            {
                [Fact]
                public void ReturnsALoadDirective()
                {
                    var model = new DisplayPackageViewModel
                    {
                        Id = "Cake.Recipe",
                        Version = "1.0.0",
                        Tags = new[] { "cake-recipe" },
                    };

                    var actual = model.GetCakeInstallPackageCommand();

                    Assert.Equal("#load nuget:?package=Cake.Recipe&version=1.0.0", actual);
                }

                [Fact]
                public void ReturnsALoadDirectiveWithPrerelease()
                {
                    var model = new DisplayPackageViewModel
                    {
                        Id = "Cake.Recipe",
                        Version = "1.0.0",
                        Tags = new[] { "cake-recipe" },
                        Prerelease = true,
                    };

                    var actual = model.GetCakeInstallPackageCommand();

                    Assert.Equal("#load nuget:?package=Cake.Recipe&version=1.0.0&prerelease", actual);
                }
            }

            public class GivenAPackageWithoutAnyKnownCakeTags
            {
                [Theory]
                [ClassData(typeof(PackageTagsNotKnownToBeCakeExtensions))]
                public void ReturnsMultipleDirectives(string[] tags)
                {
                    var model = new DisplayPackageViewModel
                    {
                        Id = "Polly",
                        Version = "1.0.0",
                        Tags = tags,
                    };

                    var actual = model.GetCakeInstallPackageCommand();

                    Assert.Contains("#addin nuget:?package=Polly&version=1.0.0", actual);
                    Assert.Contains("#tool nuget:?package=Polly&version=1.0.0", actual);
                }

                [Theory]
                [ClassData(typeof(PackageTagsNotKnownToBeCakeExtensions))]
                public void ReturnsMultipleDirectivesWithPrerelease(string[] tags)
                {
                    var model = new DisplayPackageViewModel
                    {
                        Id = "Polly",
                        Version = "1.0.0",
                        Tags = tags,
                        Prerelease = true,
                    };

                    var actual = model.GetCakeInstallPackageCommand();

                    Assert.Contains("#addin nuget:?package=Polly&version=1.0.0&prerelease", actual);
                    Assert.Contains("#tool nuget:?package=Polly&version=1.0.0&prerelease", actual);
                }
            }
        }

        public class PackageTagsKnownToBeCakeExtensions : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new[] { "cake-addin" } };
                yield return new object[] { new[] { "cake-module" } };
                yield return new object[] { new[] { "cake-recipe" } };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class PackageTagsNotKnownToBeCakeExtensions : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { null };
                yield return new object[] { new string[0] };
                yield return new object[] { new[] { "json" } };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}

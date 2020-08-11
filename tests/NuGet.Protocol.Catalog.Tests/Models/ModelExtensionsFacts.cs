// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Catalog
{
    public class ModelExtensionsFacts
    {
        public class ParseRange
        {
            [Theory]
            [InlineData("")]
            [InlineData("  ")]
            [InlineData("\r\n")]
            [InlineData(null)]
            [InlineData("0.0.0-~4")]
            [InlineData("(, )")]
            public void ReturnsAllRangeForMissingOrInvalid(string range)
            {
                var packageDependency = new PackageDependency
                {
                    Range = range,
                };

                var output = packageDependency.ParseRange();

                Assert.Equal(VersionRange.All, output);
            }
        }

        public class IsListed
        {
            [Theory]
            [InlineData("1900-01-01Z", false)]
            [InlineData("1900-01-01Z", true)]
            [InlineData("2018-01-01Z", false)]
            [InlineData("2018-01-01Z", true)]
            public void PrefersListedPropertyOverPublished(string published, bool listed)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    Listed = listed,
                    Published = DateTimeOffset.Parse(published),
                };

                var actual = leaf.IsListed();

                Assert.Equal(listed, actual);
            }

            [Theory]
            [InlineData("1899-01-01Z", true)]
            [InlineData("1900-01-01Z", false)]
            [InlineData("1900-01-01T00:00:01Z", false)]
            [InlineData("1900-01-02Z", false)]
            [InlineData("1900-02-01Z", false)]
            [InlineData("1901-01-01Z", true)]
            [InlineData("2018-01-01Z", true)]
            public void PrefersListedProperty(string published, bool listed)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    Published = DateTimeOffset.Parse(published),
                };

                var actual = leaf.IsListed();

                Assert.Equal(listed, actual);
            }
        }

        public class IsSemVer2
        {
            [Theory]
            [InlineData("1.0.0+git", true)]
            [InlineData("1.0.0-alpha.1", true)]
            [InlineData("1.0.0-alpha.1+git", true)]
            [InlineData("1.0.0", false)]
            public void SemVer2PackageVersionMeansSemVer2(string packageVersion, bool isSemVer2)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageVersion = packageVersion,
                    VerbatimVersion = "1.0.0",
                };

                var actual = leaf.IsSemVer2();

                Assert.Equal(isSemVer2, actual);
            }

            [Fact]
            public void AllowsNullVerbatimVersion()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageVersion = "1.0.0",
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            Dependencies = new List<PackageDependency>
                            {
                                new PackageDependency
                                {
                                    Range = "[1.0.0, )",
                                },
                            },
                        },
                    },
                };

                var actual = leaf.IsSemVer2();

                Assert.False(actual);
            }

            [Fact]
            public void AllowsNullDependencyGroups()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageVersion = "1.0.0",
                    VerbatimVersion = "1.0.0",
                };

                var actual = leaf.IsSemVer2();

                Assert.False(actual);
            }

            [Fact]
            public void AllowsNullDependencies()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageVersion = "1.0.0",
                    VerbatimVersion = "1.0.0",
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup(),
                    }
                };

                var actual = leaf.IsSemVer2();

                Assert.False(actual);
            }

            [Theory]
            [InlineData("1.0.0+git", true)]
            [InlineData("1.0.0-alpha.1", true)]
            [InlineData("1.0.0-alpha.1+git", true)]
            [InlineData("1.0.0", false)]
            public void SemVer2VerbatimVersionMeansSemVer2(string verbatimVersion, bool isSemVer2)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageVersion = "1.0.0",
                    VerbatimVersion = verbatimVersion,
                };

                var actual = leaf.IsSemVer2();

                Assert.Equal(isSemVer2, actual);
            }


            [Theory]
            [InlineData("1.0.0+git", true)]
            [InlineData("1.0.0-alpha.1", true)]
            [InlineData("1.0.0-alpha.1+git", true)]
            [InlineData("[1.0.0-alpha.1+git, )", true)]
            [InlineData("(, 1.0.0-alpha.1+git)", true)]
            [InlineData("(0.0.0+git, 1.0.0-alpha.1+git)", true)]
            [InlineData("[1.0.0-alpha.1+git]", true)]
            [InlineData("1.0.0", false)]
            [InlineData("[1.0.0, )", false)]
            [InlineData("(, 1.0.0]", false)]
            public void SemVer2DependencyVersionRangeMeansSemVer2(string range, bool isSemVer2)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageVersion = "1.0.0",
                    VerbatimVersion = "1.0.0",
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            Dependencies = new List<PackageDependency>
                            {
                                new PackageDependency
                                {
                                    Range = "0.0.0",
                                },
                            },
                        },
                        new PackageDependencyGroup
                        {
                            Dependencies = new List<PackageDependency>
                            {
                                new PackageDependency
                                {
                                    Range = "0.0.1",
                                },
                                new PackageDependency
                                {
                                    Range = range,
                                },
                            },
                        },
                    },
                };

                var actual = leaf.IsSemVer2();

                Assert.Equal(isSemVer2, actual);
            }
        }
    }
}

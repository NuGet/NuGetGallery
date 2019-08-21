// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Entities;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class BaseDocumentBuilderFacts
    {
        public class PopulateMetadataWithCatalogLeaf : Facts
        {
            [Theory]
            [InlineData("any")]
            [InlineData("agnostic")]
            [InlineData("unsupported")]
            [InlineData("fakeframework1.0")]
            public void DoesNotIncludeDependencyVersionSpecialFrameworks(string framework)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = framework,
                            Dependencies = new List<Protocol.Catalog.PackageDependency>
                            {
                                new Protocol.Catalog.PackageDependency
                                {
                                    Id = "NuGet.Versioning",
                                    Range = "2.0.0",
                                },
                                new Protocol.Catalog.PackageDependency
                                {
                                    Id = "NuGet.Frameworks",
                                    Range = "3.0.0",
                                },
                            },
                        },
                    },
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal("NuGet.Versioning:2.0.0|NuGet.Frameworks:3.0.0", full.FlattenedDependencies);
            }

            [Theory]
            [InlineData("any", ":")]
            [InlineData("net40", "::net40")]
            [InlineData("NET45", "::net45")]
            [InlineData(".NETFramework,Version=4.0", "::net40")]
            public void AddsEmptyDependencyGroup(string framework, string expected)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = framework,
                        },
                    },
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal(expected, full.FlattenedDependencies);
            }

            [Fact]
            public void AddsEmptyStringForDependencyVersionAllRange()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = "net40",
                            Dependencies = new List<Protocol.Catalog.PackageDependency>
                            {
                                new Protocol.Catalog.PackageDependency
                                {
                                    Id = "NuGet.Versioning",
                                    Range = "(, )"
                                }
                            },
                        },
                    },
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal("NuGet.Versioning::net40", full.FlattenedDependencies);
            }

            [Theory]
            [InlineData("1.0.0", "1.0.0")]
            [InlineData("1.0.0-beta+git", "1.0.0-beta")]
            [InlineData("[1.0.0-beta+git, )", "1.0.0-beta")]
            [InlineData("[1.0.0, 1.0.0]", "[1.0.0]")]
            [InlineData("[1.0.0, 2.0.0)", "[1.0.0, 2.0.0)")]
            [InlineData("[1.0.0, )", "1.0.0")]
            public void AddShortFormOfDependencyVersionRange(string input, string expected)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = "net40",
                            Dependencies = new List<Protocol.Catalog.PackageDependency>
                            {
                                new Protocol.Catalog.PackageDependency
                                {
                                    Id = "NuGet.Versioning",
                                    Range = input
                                }
                            },
                        },
                    },
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal("NuGet.Versioning:" + expected + ":net40", full.FlattenedDependencies);
            }

            [Fact]
            public void AllowNullDependencyGroups()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = null
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Null(full.FlattenedDependencies);
            }

            [Fact]
            public void IfLeafHasIconFile_LinksToFlatContainer()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    IconFile = "iconFile",
                    IconUrl = "iconUrl"
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal(FlatContainerIconPath, full.IconUrl);
            }

            [Fact]
            public void IfLeafDoesNotHaveIconFile_UsesIconUrl()
            {
                var iconUrl = "iconUrl";
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    IconUrl = iconUrl
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal(iconUrl, full.IconUrl);
            }
        }

        public class PopulateMetadataWithPackage : Facts
        {
            [Fact]
            public void IfPackageHasEmbeddedIcon_LinksToFlatContainer()
            {
                var package = new Package
                {
                    NormalizedVersion = Data.NormalizedVersion,
                    IconUrl = "iconUrl",
                    HasEmbeddedIcon = true
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.PackageId, package);

                Assert.Equal(FlatContainerIconPath, full.IconUrl);
            }

            [Fact]
            public void IfPackageDoesNotHaveEmbeddedIcon_UsesIconUrl()
            {
                var iconUrl = "iconUrl";
                var package = new Package
                {
                    NormalizedVersion = Data.NormalizedVersion,
                    IconUrl = iconUrl
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.PackageId, package);

                Assert.Equal(iconUrl, full.IconUrl);
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                Options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                Config = new AzureSearchJobConfiguration
                {
                    GalleryBaseUrl = Data.GalleryBaseUrl,
                    FlatContainerBaseUrl = Data.FlatContainerBaseUrl,
                    FlatContainerContainerName = Data.FlatContainerContainerName,
                };

                Options.Setup(o => o.Value).Returns(() => Config);

                Target = new BaseDocumentBuilder(Options.Object);
            }

            public Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> Options { get; }
            public AzureSearchJobConfiguration Config { get; }
            public BaseDocumentBuilder Target { get; }

            public static string FlatContainerIconPath =
                $"{Data.FlatContainerBaseUrl}{Data.FlatContainerContainerName}/{Data.PackageId.ToLowerInvariant()}/{Data.NormalizedVersion.ToLowerInvariant()}/icon";
        }
    }
}

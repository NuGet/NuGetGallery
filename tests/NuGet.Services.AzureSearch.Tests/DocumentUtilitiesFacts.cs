// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureSearch.Support;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class DocumentUtilitiesFacts
    {
        private static readonly Uri GalleryBaseUrl = new Uri(Data.GalleryBaseUrl, UriKind.Absolute);

        public class GetHijackDocumentKey
        {
            [Theory]
            [InlineData("NuGet.Versioning", "1.0.0", "nuget_versioning_1_0_0-bnVnZXQudmVyc2lvbmluZy8xLjAuMA2")]
            [InlineData("nuget.versioning", "1.0.0", "nuget_versioning_1_0_0-bnVnZXQudmVyc2lvbmluZy8xLjAuMA2")]
            [InlineData("NUGET.VERSIONING", "1.0.0", "nuget_versioning_1_0_0-bnVnZXQudmVyc2lvbmluZy8xLjAuMA2")]
            [InlineData("_", "1.0.0", "1_0_0-Xy8xLjAuMA2")]
            [InlineData("foo-bar", "1.0.0", "foo-bar_1_0_0-Zm9vLWJhci8xLjAuMA2")]
            [InlineData("İzmir", "1.0.0", "zmir_1_0_0-xLB6bWlyLzEuMC4w0")]
            [InlineData("İİzmir", "1.0.0", "zmir_1_0_0-xLDEsHptaXIvMS4wLjA1")]
            [InlineData("zİİmir", "1.0.0", "z__mir_1_0_0-esSwxLBtaXIvMS4wLjA1")]
            [InlineData("zmirİ", "1.0.0", "zmir__1_0_0-em1pcsSwLzEuMC4w0")]
            [InlineData("zmirİİ", "1.0.0", "zmir___1_0_0-em1pcsSwxLAvMS4wLjA1")]
            [InlineData("惡", "1.0.0", "1_0_0-5oOhLzEuMC4w0")]
            [InlineData("jQuery", "1.0.0-alpha", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-Alpha", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-ALPHA", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-ALPHA.1", "jquery_1_0_0-alpha_1-anF1ZXJ5LzEuMC4wLWFscGhhLjE1")]
            public void EncodesHijackDocumentKey(string id, string version, string expected)
            {
                var actual = DocumentUtilities.GetHijackDocumentKey(id, version);

                Assert.Equal(expected, actual);
            }
        }

        public class GetSearchDocumentKey
        {
            [Theory]
            [InlineData("NuGet.Versioning", "nuget_versioning-bnVnZXQudmVyc2lvbmluZw2")]
            [InlineData("nuget.versioning", "nuget_versioning-bnVnZXQudmVyc2lvbmluZw2")]
            [InlineData("NUGET.VERSIONING", "nuget_versioning-bnVnZXQudmVyc2lvbmluZw2")]
            [InlineData("_", "Xw2")]
            [InlineData("foo-bar", "foo-bar-Zm9vLWJhcg2")]
            [InlineData("İzmir", "zmir-xLB6bWly0")]
            [InlineData("İİzmir", "zmir-xLDEsHptaXI1")]
            [InlineData("zİİmir", "z__mir-esSwxLBtaXI1")]
            [InlineData("zmirİ", "zmir_-em1pcsSw0")]
            [InlineData("zmirİİ", "zmir__-em1pcsSwxLA1")]
            [InlineData("惡", "5oOh0")]
            public void EncodesSearchDocumentKey(string id, string expected)
            {
                foreach (var searchFilters in Enum.GetValues(typeof(SearchFilters)).Cast<SearchFilters>())
                {
                    var actual = DocumentUtilities.GetSearchDocumentKey(id, searchFilters);

                    Assert.Equal(expected + "-" + searchFilters, actual);
                }
            }
        }

        public class PopulateMetadataWithCatalogLeaf
        {
            private const string NormalizedVersion = "1.0.0";

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
                    PackageVersion = NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = framework,
                            Dependencies = new List<PackageDependency>
                            {
                                new PackageDependency
                                {
                                    Id = "NuGet.Versioning",
                                    Range = "2.0.0",
                                },
                                new PackageDependency
                                {
                                    Id = "NuGet.Frameworks",
                                    Range = "3.0.0",
                                },
                            },
                        },
                    },
                };
                var full = new HijackDocument.Full();

                DocumentUtilities.PopulateMetadata(full, NormalizedVersion, leaf, GalleryBaseUrl);

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
                    PackageVersion = NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = framework,
                        },
                    },
                };
                var full = new HijackDocument.Full();

                DocumentUtilities.PopulateMetadata(full, NormalizedVersion, leaf, GalleryBaseUrl);

                Assert.Equal(expected, full.FlattenedDependencies);
            }

            [Fact]
            public void AddsEmptyStringForDependencyVersionAllRange()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = "net40",
                            Dependencies = new List<PackageDependency>
                            {
                                new PackageDependency
                                {
                                    Id = "NuGet.Versioning",
                                    Range = "(, )"
                                }
                            },
                        },
                    },
                };
                var full = new HijackDocument.Full();

                DocumentUtilities.PopulateMetadata(full, NormalizedVersion, leaf, GalleryBaseUrl);

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
                    PackageVersion = NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = "net40",
                            Dependencies = new List<PackageDependency>
                            {
                                new PackageDependency
                                {
                                    Id = "NuGet.Versioning",
                                    Range = input
                                }
                            },
                        },
                    },
                };
                var full = new HijackDocument.Full();

                DocumentUtilities.PopulateMetadata(full, NormalizedVersion, leaf, GalleryBaseUrl);

                Assert.Equal("NuGet.Versioning:" + expected + ":net40", full.FlattenedDependencies);
            }

            [Fact]
            public void AllowNullDependencyGroups()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = NormalizedVersion,
                    DependencyGroups = null
                };
                var full = new HijackDocument.Full();

                DocumentUtilities.PopulateMetadata(full, NormalizedVersion, leaf, GalleryBaseUrl);

                Assert.Null(full.FlattenedDependencies);
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch
{
    public class SearchDocumentBuilderFacts
    {
        public class LatestFlagsOrNull : BaseFacts
        {
            public LatestFlagsOrNull(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [InlineData(SearchFilters.Default)]
            [InlineData(SearchFilters.IncludeSemVer2)]
            public void ExcludePrereleaseWithOnlyOnePrereleaseVersion(SearchFilters searchFilters)
            {
                var versionLists = VersionLists("1.0.0-alpha");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Null(actual);
            }

            [Theory]
            [InlineData(SearchFilters.Default)]
            [InlineData(SearchFilters.IncludePrerelease)]
            public void ExcludingSemVer2WithOnlySemVer2(SearchFilters searchFilters)
            {
                var versionLists = VersionLists("1.0.0+git", "2.0.0-alpha.1");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Null(actual);
            }

            [Theory]
            [InlineData(SearchFilters.IncludePrerelease)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2)]
            public void IncludePrereleaseWithOnlyOnePrereleaseVersion(SearchFilters searchFilters)
            {
                var versionLists = VersionLists("1.0.0-alpha");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Equal("1.0.0-alpha", actual.LatestVersionInfo.FullVersion);
                Assert.False(actual.IsLatestStable);
                Assert.True(actual.IsLatest);
            }

            [Theory]
            [InlineData(SearchFilters.Default)]
            [InlineData(SearchFilters.IncludePrerelease)]
            [InlineData(SearchFilters.IncludeSemVer2)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2)]
            public void OnlyOneStableVersion(SearchFilters searchFilters)
            {
                var versionLists = VersionLists("1.0.0");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Equal("1.0.0", actual.LatestVersionInfo.FullVersion);
                Assert.True(actual.IsLatestStable);
                Assert.True(actual.IsLatest);
            }

            [Theory]
            [InlineData(SearchFilters.Default, "1.0.0", true, false)]
            [InlineData(SearchFilters.IncludeSemVer2, "1.0.0", true, false)]
            [InlineData(SearchFilters.IncludePrerelease, "2.0.0-alpha", false, true)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, "2.0.0-alpha", false, true)]
            public void LatestIsPrereleaseWithLowerStable(SearchFilters searchFilters, string latest, bool isLatestStable, bool isLatest)
            {
                var versionLists = VersionLists("1.0.0", "2.0.0-alpha");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Equal(latest, actual.LatestVersionInfo.FullVersion);
                Assert.Equal(isLatestStable, actual.IsLatestStable);
                Assert.Equal(isLatest, actual.IsLatest);
            }

            [Theory]
            [InlineData(SearchFilters.Default, "1.0.0", true, false)]
            [InlineData(SearchFilters.IncludePrerelease, "2.0.0-alpha", false, true)]
            [InlineData(SearchFilters.IncludeSemVer2, "3.0.0+git", true, false)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, "4.0.0-beta.1", false, true)]
            public void AllVersionTypes(SearchFilters searchFilters, string latest, bool isLatestStable, bool isLatest)
            {
                var versionLists = VersionLists("1.0.0", "2.0.0-alpha", "3.0.0+git", "4.0.0-beta.1");

                var actual = _target.LatestFlagsOrNull(versionLists, searchFilters);

                Assert.Equal(latest, actual.LatestVersionInfo.FullVersion);
                Assert.Equal(isLatestStable, actual.IsLatestStable);
                Assert.Equal(isLatest, actual.IsLatest);
            }

            private static VersionLists VersionLists(params string[] versions)
            {
                return new VersionLists(new VersionListData(versions
                    .Select(x => NuGetVersion.Parse(x))
                    .ToDictionary(x => x.ToFullString(), x => new VersionPropertiesData(listed: true, semVer2: x.IsSemVer2))));
            }
        }

        public class Keyed : BaseFacts
        {
            public Keyed(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.Keyed(Data.PackageId, Data.SearchFilters);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateOwners : BaseFacts
        {
            public UpdateOwners(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.UpdateOwners(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Owners);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00\u002B00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument\u002BUpdateOwners"",
      ""lastUpdatedFromCatalog"": false,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateDownloadCount : BaseFacts
        {
            public UpdateDownloadCount(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.UpdateDownloadCount(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.TotalDownloadCount);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""totalDownloadCount"": 1001,
      ""downloadScore"": 0.14381174563233068,
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00\u002B00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument\u002BUpdateDownloadCount"",
      ""lastUpdatedFromCatalog"": false,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateVersionListFromCatalog : BaseFacts
        {
            public UpdateVersionListFromCatalog(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public async Task SetsExpectedProperties(bool isLatestStable, bool isLatest)
            {
                var document = _target.UpdateVersionListFromCatalog(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.CommitTimestamp,
                    Data.CommitId,
                    Data.Versions,
                    isLatestStable,
                    isLatest);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""versions"": [
        ""1.0.0"",
        ""2.0.0\u002Bgit"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha\u002Bgit""
      ],
      ""isLatestStable"": " + isLatestStable.ToString().ToLowerInvariant() + @",
      ""isLatest"": " + isLatest.ToString().ToLowerInvariant() + @",
      ""lastCommitTimestamp"": ""2018-12-13T12:30:00\u002B00:00"",
      ""lastCommitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00\u002B00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument\u002BUpdateVersionList"",
      ""lastUpdatedFromCatalog"": true,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateVersionListAndOwnersFromCatalog : BaseFacts
        {
            public UpdateVersionListAndOwnersFromCatalog(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public async Task SetsExpectedProperties(bool isLatestStable, bool isLatest)
            {
                var document = _target.UpdateVersionListAndOwnersFromCatalog(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.CommitTimestamp,
                    Data.CommitId,
                    Data.Versions,
                    isLatestStable,
                    isLatest,
                    Data.Owners);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""versions"": [
        ""1.0.0"",
        ""2.0.0\u002Bgit"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha\u002Bgit""
      ],
      ""isLatestStable"": " + isLatestStable.ToString().ToLowerInvariant() + @",
      ""isLatest"": " + isLatest.ToString().ToLowerInvariant() + @",
      ""lastCommitTimestamp"": ""2018-12-13T12:30:00\u002B00:00"",
      ""lastCommitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00\u002B00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument\u002BUpdateVersionListAndOwners"",
      ""lastUpdatedFromCatalog"": true,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateLatestFromCatalog : BaseFacts
        {
            public UpdateLatestFromCatalog(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdWhenMissingForTitle(string title)
            {
                var leaf = Data.Leaf;
                leaf.Title = title;

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.PackageId, document.Title);
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesLowerIdWhenMissingForSortableTitle(string title)
            {
                var leaf = Data.Leaf;
                leaf.Title = title;

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.PackageId.ToLowerInvariant(), document.SortableTitle);
            }

            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public async Task SetsExpectedProperties(SearchFilters searchFilters, string expected)
            {
                var document = _target.UpdateLatestFromCatalog(
                    searchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: Data.Leaf,
                    owners: Data.Owners);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""searchFilters"": """ + expected + @""",
      ""filterablePackageTypes"": [
        ""dependency""
      ],
      ""fullVersion"": ""7.1.2-alpha\u002Bgit"",
      ""versions"": [
        ""1.0.0"",
        ""2.0.0\u002Bgit"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha\u002Bgit""
      ],
      ""packageTypes"": [
        ""Dependency""
      ],
      ""frameworks"": [
        ""netframework""
      ],
      ""tfms"": [
        ""net40-client""
      ],
      ""computedFrameworks"": [
        ""netframework""
      ],
      ""computedTfms"": [
        ""net40-client""
      ],
      ""isLatestStable"": false,
      ""isLatest"": true,
      ""deprecation"": {
        ""alternatePackage"": {
          ""id"": ""test.alternatepackage"",
          ""range"": ""[1.0.0, )""
        },
        ""message"": ""test message for test.alternatepackage-1.0.0"",
        ""reasons"": [
          ""Other"",
          ""Legacy""
        ]
      },
      ""vulnerabilities"": [
        {
          ""advisoryURL"": ""test AdvisoryUrl for Low Severity"",
          ""severity"": 0
        },
        {
          ""advisoryURL"": ""test AdvisoryUrl for Moderate Severity"",
          ""severity"": 1
        }
      ],
      ""semVerLevel"": 2,
      ""authors"": ""Microsoft"",
      ""copyright"": ""\u00A9 Microsoft Corporation. All rights reserved."",
      ""created"": ""2017-01-01T00:00:00\u002B00:00"",
      ""description"": ""Description."",
      ""fileSize"": 3039254,
      ""flattenedDependencies"": ""Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client"",
      ""hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33\u002BZ/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""hashAlgorithm"": ""SHA512"",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""language"": ""en-US"",
      ""lastEdited"": ""2017-01-02T00:00:00\u002B00:00"",
      ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""minClientVersion"": ""2.12"",
      ""normalizedVersion"": ""7.1.2-alpha"",
      ""originalVersion"": ""7.1.2.0-alpha\u002Bgit"",
      ""packageId"": ""WindowsAzure.Storage"",
      ""prerelease"": true,
      ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
      ""published"": ""2017-01-03T00:00:00\u002B00:00"",
      ""releaseNotes"": ""Release notes."",
      ""requiresLicenseAcceptance"": true,
      ""sortableTitle"": ""windows azure storage"",
      ""summary"": ""Summary."",
      ""tags"": [
        ""Microsoft"",
        ""Azure"",
        ""Storage"",
        ""Table"",
        ""Blob"",
        ""File"",
        ""Queue"",
        ""Scalable"",
        ""windowsazureofficial""
      ],
      ""title"": ""Windows Azure Storage"",
      ""tokenizedPackageId"": ""WindowsAzure.Storage"",
      ""lastCommitTimestamp"": ""2018-12-13T12:30:00\u002B00:00"",
      ""lastCommitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00\u002B00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument\u002BUpdateLatest"",
      ""lastUpdatedFromCatalog"": true,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-" + expected + @"""
    }
  ]
}", json);
            }

            [Theory]
            [MemberData(nameof(CatalogPackageTypesData))]
            public void SetsExpectedPackageTypes(List<NuGet.Protocol.Catalog.PackageType> packageTypes, string[] expectedFilterable, string[] expectedDisplay)
            {
                var leaf = Data.Leaf;
                leaf.PackageTypes = packageTypes;

                var document = _target.UpdateLatestFromCatalog(
                    SearchFilters.Default,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                SetDocumentLastUpdated(document);
                Assert.Equal(document.FilterablePackageTypes.Length, document.PackageTypes.Length);
                Assert.Equal(expectedFilterable, document.FilterablePackageTypes);
                Assert.Equal(expectedDisplay, document.PackageTypes);
            }

            [Fact]
            public void LeavesNullRequiresLicenseAcceptanceAsNull()
            {
                var leaf = Data.Leaf;
                leaf.RequireLicenseAcceptance = null;

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Null(document.RequiresLicenseAcceptance);
            }

            [Fact]
            public void SetsLicenseUrlToGalleryWhenPackageHasLicenseExpression()
            {
                var leaf = Data.Leaf;
                leaf.LicenseExpression = "MIT";

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.GalleryLicenseUrl, document.LicenseUrl);
            }

            [Fact]
            public void SetsLicenseUrlToGalleryWhenPackageHasLicenseFile()
            {
                var leaf = Data.Leaf;
                leaf.LicenseFile = "LICENSE.txt";

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.GalleryLicenseUrl, document.LicenseUrl);
            }

            [Fact]
            public void SetsIconUrlToFlatContainerWhenPackageHasIconFileAndIconUrl()
            {
                var leaf = Data.Leaf;
                leaf.IconUrl = "https://other-example/icon.png";
                leaf.IconFile = "icon.png";

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.FlatContainerIconUrl, document.IconUrl);
            }

            [Fact]
            public void SetsIconUrlToFlatContainerWhenPackageHasIconFileAndNoIconUrl()
            {
                var leaf = Data.Leaf;
                leaf.IconUrl = null;
                leaf.IconFile = "icon.png";

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Equal(Data.FlatContainerIconUrl, document.IconUrl);
            }

            [Theory]
            [MemberData(nameof(TargetFrameworkCases))]
            public void AddsFrameworksAndTfmsFromCatalogLeaf(List<string> supportedFrameworks, List<string> expectedTfms, List<string> expectedFrameworks)
            {
                // arrange
                var leaf = Data.Leaf;
                leaf.PackageEntries = supportedFrameworks
                                                .Select(f => new NuGet.Protocol.Catalog.PackageEntry
                                                {
                                                    FullName = $"lib/{f}/{leaf.PackageId}.dll",
                                                    Name = $"{leaf.PackageId}.dll"
                                                })
                                                .ToList();

                // act
                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                // assert
                Assert.Equal(document.Tfms.Length, expectedTfms.Count);
                foreach (var item in expectedTfms)
                {
                    Assert.Contains(item, document.Tfms);
                }

                Assert.Equal(document.Frameworks.Length, expectedFrameworks.Count);
                foreach (var item in expectedFrameworks)
                {
                    Assert.Contains(item, document.Frameworks);
                }
            }

            [Theory]
            [MemberData(nameof(ComputedFrameworkCases))]
            public void AddsComputedFrameworksAndTfmsFromCatalogLeaf(List<string> supportedTfms, List<string> computedTfms, List<string> computedFrameworks)
            {
                // arrange
                var leaf = Data.Leaf;
                leaf.PackageEntries = supportedTfms
                                                .Select(f => new NuGet.Protocol.Catalog.PackageEntry
                                                {
                                                    FullName = $"lib/{f}/{leaf.PackageId}.dll",
                                                    Name = $"{leaf.PackageId}.dll"
                                                })
                                                .ToList();

                // act
                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                // assert
                Assert.True(document.ComputedTfms.Length >= computedTfms.Count);
                foreach (var item in computedTfms)
                {
                    Assert.Contains(item, document.ComputedTfms);
                }

                Assert.Equal(document.ComputedFrameworks.Length, computedFrameworks.Count);
                foreach (var item in computedFrameworks)
                {
                    Assert.Contains(item, document.ComputedFrameworks);
                }
            }

            [Theory]
            [MemberData(nameof(AdditionalCatalogTFMCases))]
            public void CalculatesAssetFrameworksFromPackageEntriesAndPackageTypes(List<NuGet.Protocol.Catalog.PackageType> packageTypes,
                                                                                   List<string> files,
                                                                                   List<string> expectedTfms,
                                                                                   List<string> expectedFrameworks)
            {
                // arrange
                var leaf = Data.Leaf;
                leaf.PackageEntries = files
                                        .Select(f => new NuGet.Protocol.Catalog.PackageEntry { FullName = f })
                                        .ToList();
                leaf.PackageTypes = packageTypes;

                // act
                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                // assert
                Assert.Equal(document.Tfms.Length, expectedTfms.Count);
                foreach (var item in expectedTfms)
                {
                    Assert.Contains(item, document.Tfms);
                }

                Assert.Equal(document.Frameworks.Length, expectedFrameworks.Count);
                foreach (var item in expectedFrameworks)
                {
                    Assert.Contains(item, document.Frameworks);
                }
            }

            [Fact]
            public void CheckNullDeprecation()
            {
                var leaf = Data.Leaf;
                leaf.Deprecation = null;

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Null(document.Deprecation);
            }

            [Fact]
            public void CheckNullDeprecationReasons()
            {
                var leaf = Data.Leaf;
                leaf.Deprecation = new Protocol.Catalog.PackageDeprecation();

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Null(document.Deprecation);
            }

            [Fact]
            public void CheckEmptyDeprecationReasons()
            {
                var leaf = Data.Leaf;
                leaf.Deprecation = new Protocol.Catalog.PackageDeprecation() { Reasons = new List<string> {} };

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.Null(document.Deprecation);
            }

            [Fact]
            public void CheckNullAlternatePackage()
            {
                var leaf = Data.Leaf;
                leaf.Deprecation = new Protocol.Catalog.PackageDeprecation() { Reasons = new List<string> {"Other"} };

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.NotNull(document.Deprecation);
                Assert.NotEmpty(document.Deprecation.Reasons);
                Assert.Null(document.Deprecation.AlternatePackage);
            }

            [Fact]
            public void CheckNullVulnerabilities()
            {
                var leaf = Data.Leaf;
                leaf.Vulnerabilities = null;

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.NotNull(document.Vulnerabilities);
                Assert.Empty(document.Vulnerabilities);
            }

            [Fact]
            public void CheckEmptyVulnerabilities()
            {
                var leaf = Data.Leaf;
                leaf.Vulnerabilities = new List<Protocol.Catalog.PackageVulnerability>();

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.NotNull(document.Vulnerabilities);
                Assert.Empty(document.Vulnerabilities);
            }

            [Fact]
            public void CheckNullInVulnerabilitiesList()
            {
                var leaf = Data.Leaf;
                leaf.Vulnerabilities = new List<Protocol.Catalog.PackageVulnerability>();
                leaf.Vulnerabilities.Add(null);

                var document = _target.UpdateLatestFromCatalog(
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    normalizedVersion: Data.NormalizedVersion,
                    fullVersion: Data.FullVersion,
                    leaf: leaf,
                    owners: Data.Owners);

                Assert.NotNull(document.Vulnerabilities);
                Assert.Empty(document.Vulnerabilities);
            }
        }

        public class FullFromDb : BaseFacts
        {
            public FullFromDb(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void NormalizesSortableTitle()
            {
                var package = Data.PackageEntity;
                package.Title = "  Some Title ";

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal("some title", document.SortableTitle);
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdWhenMissingForTitle(string title)
            {
                var package = Data.PackageEntity;
                package.Title = title;

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(Data.PackageId, document.Title);
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesLowerIdWhenMissingForSortableTitle(string title)
            {
                var package = Data.PackageEntity;
                package.Title = title;

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(Data.PackageId.ToLowerInvariant(), document.SortableTitle);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void SetsIsExcludedByDefaultPropertyCorrectly(bool shouldBeExcluded)
            {
                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: Data.PackageEntity,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: shouldBeExcluded);

                Assert.Equal(shouldBeExcluded, document.IsExcludedByDefault);
            }

            [Fact]
            public async Task SerializesNullSemVerLevel()
            {
                var package = Data.PackageEntity;
                package.SemVerLevelKey = SemVerLevelKey.Unknown;

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Contains("\"semVerLevel\": null,", json);
            }

            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public async Task SetsExpectedProperties(SearchFilters searchFilters, string expected)
            {
                var document = _target.FullFromDb(
                    Data.PackageId,
                    searchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: Data.PackageEntity,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                SetDocumentLastUpdated(document);
                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""totalDownloadCount"": 1001,
      ""downloadScore"": 0.14381174563233068,
      ""isExcludedByDefault"": false,
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""searchFilters"": """ + expected + @""",
      ""filterablePackageTypes"": [
        ""dependency""
      ],
      ""fullVersion"": ""7.1.2-alpha\u002Bgit"",
      ""versions"": [
        ""1.0.0"",
        ""2.0.0\u002Bgit"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha\u002Bgit""
      ],
      ""packageTypes"": [
        ""Dependency""
      ],
      ""frameworks"": [
        ""netframework""
      ],
      ""tfms"": [
        ""net40-client""
      ],
      ""computedFrameworks"": [
        ""netframework""
      ],
      ""computedTfms"": [
        ""net40-client""
      ],
      ""isLatestStable"": false,
      ""isLatest"": true,
      ""deprecation"": {
        ""alternatePackage"": {
          ""id"": ""test.alternatepackage"",
          ""range"": ""[1.0.0, )""
        },
        ""message"": ""test message for test.alternatepackage-1.0.0"",
        ""reasons"": [
          ""Other"",
          ""Legacy""
        ]
      },
      ""vulnerabilities"": [
        {
          ""advisoryURL"": ""test AdvisoryUrl for Low Severity"",
          ""severity"": 0
        },
        {
          ""advisoryURL"": ""test AdvisoryUrl for Moderate Severity"",
          ""severity"": 1
        }
      ],
      ""semVerLevel"": 2,
      ""authors"": ""Microsoft"",
      ""copyright"": ""\u00A9 Microsoft Corporation. All rights reserved."",
      ""created"": ""2017-01-01T00:00:00\u002B00:00"",
      ""description"": ""Description."",
      ""fileSize"": 3039254,
      ""flattenedDependencies"": ""Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client"",
      ""hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33\u002BZ/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""hashAlgorithm"": ""SHA512"",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""language"": ""en-US"",
      ""lastEdited"": ""2017-01-02T00:00:00\u002B00:00"",
      ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""minClientVersion"": ""2.12"",
      ""normalizedVersion"": ""7.1.2-alpha"",
      ""originalVersion"": ""7.1.2.0-alpha\u002Bgit"",
      ""packageId"": ""WindowsAzure.Storage"",
      ""prerelease"": true,
      ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
      ""published"": ""2017-01-03T00:00:00\u002B00:00"",
      ""releaseNotes"": ""Release notes."",
      ""requiresLicenseAcceptance"": true,
      ""sortableTitle"": ""windows azure storage"",
      ""summary"": ""Summary."",
      ""tags"": [
        ""Microsoft"",
        ""Azure"",
        ""Storage"",
        ""Table"",
        ""Blob"",
        ""File"",
        ""Queue"",
        ""Scalable"",
        ""windowsazureofficial""
      ],
      ""title"": ""Windows Azure Storage"",
      ""tokenizedPackageId"": ""WindowsAzure.Storage"",
      ""lastCommitTimestamp"": null,
      ""lastCommitId"": null,
      ""lastUpdatedDocument"": ""2018-12-14T09:30:00\u002B00:00"",
      ""lastDocumentType"": ""NuGet.Services.AzureSearch.SearchDocument\u002BFull"",
      ""lastUpdatedFromCatalog"": false,
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-" + expected + @"""
    }
  ]
}", json);
            }

            [Theory]
            [MemberData(nameof(DBPackageTypesData))]
            public void SetsExpectedPackageTypes(List<PackageType> packageTypes, string[] expectedFilterable, string[] expectedDisplay)
            {
                var package = Data.PackageEntity;
                package.PackageTypes = packageTypes;

                var document = _target.FullFromDb(
                    Data.PackageId,
                    SearchFilters.Default,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                SetDocumentLastUpdated(document);
                Assert.Equal(document.FilterablePackageTypes.Length, document.PackageTypes.Length);
                Assert.Equal(expectedFilterable, document.FilterablePackageTypes);
                Assert.Equal(expectedDisplay, document.PackageTypes);
            }

            [Fact]
            public void SplitsTags()
            {
                var package = Data.PackageEntity;
                package.Tags = "foo; BAR |     Baz";

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(new[] { "foo", "BAR", "Baz" }, document.Tags);
            }

            [Fact]
            public void SetsLicenseUrlToGalleryWhenPackageHasLicenseExpression()
            {
                var package = Data.PackageEntity;
                package.LicenseExpression = "MIT";

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(Data.GalleryLicenseUrl, document.LicenseUrl);
            }

            [Theory]
            [InlineData(EmbeddedLicenseFileType.PlainText)]
            [InlineData(EmbeddedLicenseFileType.Markdown)]
            public void SetsLicenseUrlToGalleryWhenPackageHasLicenseFile(EmbeddedLicenseFileType type)
            {
                var package = Data.PackageEntity;
                package.EmbeddedLicenseType = type;

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Equal(Data.GalleryLicenseUrl, document.LicenseUrl);
            }

            [Theory]
            [MemberData(nameof(TargetFrameworkCases))]
            [MemberData(nameof(AdditionalPackageTFMCases))]
            public void AddsFrameworksAndTfmsFromPackage(List<string> supportedFrameworks, List<string> expectedTfms, List<string> expectedFrameworks)
            {
                // arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "TestPackage",
                    },
                    Id = "TestPackage",
                    NormalizedVersion = Data.NormalizedVersion,
                    LicenseExpression = "Unlicense",
                    HasEmbeddedIcon = true,
                    SupportedFrameworks = supportedFrameworks
                                                .Select(f => new PackageFramework() { TargetFramework = f })
                                                .ToArray(),
                };

                // act
                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                // assert
                Assert.Equal(document.Tfms.Length, expectedTfms.Count);
                foreach (var item in expectedTfms)
                {
                    Assert.Contains(item, document.Tfms);
                }

                Assert.Equal(document.Frameworks.Length, expectedFrameworks.Count);
                foreach (var item in expectedFrameworks)
                {
                    Assert.Contains(item, document.Frameworks);
                }
            }

            [Theory]
            [MemberData(nameof(ComputedFrameworkCases))]
            public void AddsComputedFrameworksAndTfmsFromPackage(List<string> supportedTfms, List<string> computedTfms, List<string> computedFrameworks)
            {
                // arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "TestPackage",
                    },
                    Id = "TestPackage",
                    NormalizedVersion = Data.NormalizedVersion,
                    LicenseExpression = "Unlicense",
                    HasEmbeddedIcon = true,
                    SupportedFrameworks = supportedTfms
                                                .Select(f => new PackageFramework() { TargetFramework = f })
                                                .ToArray(),
                };

                // act
                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                // assert
                Assert.True(document.ComputedTfms.Length >= computedTfms.Count);
                foreach (var item in computedTfms)
                {
                    Assert.Contains(item, document.ComputedTfms);
                }

                Assert.Equal(document.ComputedFrameworks.Length, computedFrameworks.Count);
                foreach (var item in computedFrameworks)
                {
                    Assert.Contains(item, document.ComputedFrameworks);
                }
            }

            [Fact]
            public void CheckNullDeprecation()
            {
                var package = Data.PackageEntity;
                package.Deprecations = null; 

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Null(document.Deprecation);
            }

            [Fact]
            public void CheckEmptyDeprecation()
            {
                var package = Data.PackageEntity;
                package.Deprecations = new List<PackageDeprecation>(); 

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Null(document.Deprecation);
            }

            [Fact]
            public void CheckNotDeprecated()
            {
                var package = Data.PackageEntity;
                package.Deprecations = new List<PackageDeprecation>(); 
                var deprecation = new PackageDeprecation() {Status = 0};
                package.Deprecations.Add(deprecation);

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Null(document.Deprecation);
            }

            [Fact]
            public void CheckMultipleDeprecations()
            {
                var package = Data.PackageEntity;
                package.Deprecations = new List<PackageDeprecation>(); 
                var deprecation = new PackageDeprecation() {Status = 0};
                package.Deprecations.Add(deprecation);
                package.Deprecations.Add(deprecation);

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.Null(document.Deprecation);
            }

            [Theory]
            [InlineData(1, new string[] {"Other"})]
            [InlineData(2, new string[] {"Legacy"})]
            [InlineData(3, new string[] {"Other", "Legacy"})]
            [InlineData(4, new string[] {"CriticalBugs"})]
            [InlineData(5, new string[] {"Other", "CriticalBugs"})]
            [InlineData(6, new string[] {"Legacy", "CriticalBugs"})]
            [InlineData(7, new string[] {"Other", "Legacy", "CriticalBugs"})]
            public void CheckExpectedDeprecationStatus(int status, string[] expected)
            {
                var package = Data.PackageEntity;
                package.Deprecations = new List<PackageDeprecation>(); 
                var deprecation = new PackageDeprecation() {Status = (PackageDeprecationStatus)status};
                package.Deprecations.Add(deprecation);

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);
                
                Assert.Equal(expected, document.Deprecation.Reasons);
            }

            [Fact]
            public void CheckNullAlternatePackage()
            {
                var package = Data.PackageEntity;
                package.Deprecations = new List<PackageDeprecation>(); 
                var deprecation = new PackageDeprecation() {Status = PackageDeprecationStatus.Other};
                package.Deprecations.Add(deprecation);

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.NotNull(document.Deprecation);
                Assert.Null(document.Deprecation.AlternatePackage);
            }

            [Theory]
            [InlineData(null, "*")]
            [InlineData("", "*")]
            [InlineData("1.0.0-test", "[1.0.0-test, )")]
            public void CheckExpectedRange(string version, string expectedRange)
            {
                var package = Data.PackageEntity;
                package.Deprecations = new List<PackageDeprecation>(); 
                
                var alternatepackage = Data.PackageEntity;
                alternatepackage.Version = version;
                alternatepackage.Id = "testId";

                var deprecation = new PackageDeprecation() 
                {
                    Status = PackageDeprecationStatus.Other, 
                    AlternatePackage = alternatepackage, 
                    CustomMessage = "testMessage" 
                };
                package.Deprecations.Add(deprecation);

                var document = _target.FullFromDb(
                    Data.PackageId,
                    Data.SearchFilters,
                    Data.Versions,
                    isLatestStable: false,
                    isLatest: true,
                    fullVersion: Data.FullVersion,
                    package: package,
                    owners: Data.Owners,
                    totalDownloadCount: Data.TotalDownloadCount,
                    isExcludedByDefault: false);

                Assert.NotNull(document.Deprecation.AlternatePackage);
                Assert.Equal(expectedRange, document.Deprecation.AlternatePackage.Range);
            }

            [Fact]
            public void CheckNullAndEmptyVulnerabilities()
            {
                //list is null
                var package = Data.PackageEntity;
                package.VulnerablePackageRanges = null;

                var document = _target.FullFromDb(
                        Data.PackageId,
                        Data.SearchFilters,
                        Data.Versions,
                        isLatestStable: false,
                        isLatest: true,
                        fullVersion: Data.FullVersion,
                        package: package,
                        owners: Data.Owners,
                        totalDownloadCount: Data.TotalDownloadCount,
                        isExcludedByDefault: false);

                Assert.Empty(document.Vulnerabilities);

                //list is empty
                package.VulnerablePackageRanges = new List<VulnerablePackageVersionRange>();

                document = _target.FullFromDb(
                        Data.PackageId,
                        Data.SearchFilters,
                        Data.Versions,
                        isLatestStable: false,
                        isLatest: true,
                        fullVersion: Data.FullVersion,
                        package: package,
                        owners: Data.Owners,
                        totalDownloadCount: Data.TotalDownloadCount,
                        isExcludedByDefault: false);

                Assert.Empty(document.Vulnerabilities);

                //list contains null element
                package.VulnerablePackageRanges = new List<VulnerablePackageVersionRange>();
                package.VulnerablePackageRanges.Add(null);

                document = _target.FullFromDb(
                        Data.PackageId,
                        Data.SearchFilters,
                        Data.Versions,
                        isLatestStable: false,
                        isLatest: true,
                        fullVersion: Data.FullVersion,
                        package: package,
                        owners: Data.Owners,
                        totalDownloadCount: Data.TotalDownloadCount,
                        isExcludedByDefault: false);

                Assert.Empty(document.Vulnerabilities);

                //PackageVulnerability is null
                package.VulnerablePackageRanges = new List<VulnerablePackageVersionRange>();
                var range = new VulnerablePackageVersionRange() { Vulnerability = null, PackageId = "testId", PackageVersionRange = "testRange" };
                package.VulnerablePackageRanges.Add(range);

                document = _target.FullFromDb(
                        Data.PackageId,
                        Data.SearchFilters,
                        Data.Versions,
                        isLatestStable: false,
                        isLatest: true,
                        fullVersion: Data.FullVersion,
                        package: package,
                        owners: Data.Owners,
                        totalDownloadCount: Data.TotalDownloadCount,
                        isExcludedByDefault: false);

                Assert.Empty(document.Vulnerabilities);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly ITestOutputHelper _output;
            protected readonly Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> _options;
            protected readonly BaseDocumentBuilder _baseDocumentBuilder;
            protected readonly AzureSearchJobConfiguration _config;
            protected readonly SearchDocumentBuilder _target;

            public static IEnumerable<object[]> MissingTitles = new[]
            {
                new object[] { null },
                new object[] { string.Empty },
                new object[] { " " },
                new object[] { " \t"},
            };

            public static IEnumerable<object[]> AllSearchFilters => new[]
            {
                new object[] { SearchFilters.Default, "Default" },
                new object[] { SearchFilters.IncludePrerelease, "IncludePrerelease" },
                new object[] { SearchFilters.IncludeSemVer2, "IncludeSemVer2" },
                new object[] { SearchFilters.IncludePrereleaseAndSemVer2, "IncludePrereleaseAndSemVer2" },
            };

            public static IEnumerable<object[]> CatalogPackageTypesData => new[]
            {
                new object[] {
                    new List<NuGet.Protocol.Catalog.PackageType> {
                        new NuGet.Protocol.Catalog.PackageType
                        {
                            Name = "DotNetCliTool"
                        }
                    },
                    new string[] { "dotnetclitool" },
                    new string[] { "DotNetCliTool" }
                },

                new object[] {
                    null,
                    new string[] { "dependency" },
                    new string[] { "Dependency" }
                },

                new object[] {
                    new List<NuGet.Protocol.Catalog.PackageType>(),
                    new string[] { "dependency" },
                    new string[] { "Dependency" }
                },

                new object[] {
                    new List<NuGet.Protocol.Catalog.PackageType> {
                        new NuGet.Protocol.Catalog.PackageType
                        {
                            Name = "DotNetCliTool"
                        },
                        new NuGet.Protocol.Catalog.PackageType
                        {
                            Name = "Dependency"
                        }
                    },
                    new string[] { "dotnetclitool", "dependency" },
                    new string[] { "DotNetCliTool", "Dependency" },
                },

                new object[] {
                    new List<NuGet.Protocol.Catalog.PackageType> {
                        new NuGet.Protocol.Catalog.PackageType
                        {
                            Name = "DotNetCliTool",
                            Version = "1.0.0"
                        }
                    },
                    new string[] { "dotnetclitool" },
                    new string[] { "DotNetCliTool" }
                },
            };

            public static IEnumerable<object[]> DBPackageTypesData => new[]
            {
                new object[] {
                    new List<PackageType> {
                        new PackageType
                        {
                            Name = "DotNetCliTool"
                        }
                    },
                    new string[] { "dotnetclitool" },
                    new string[] { "DotNetCliTool" }
                },

                new object[] {
                    null,
                    new string[] { "dependency" },
                    new string[] { "Dependency" }
                },

                new object[] {
                    new List<PackageType>(),
                    new string[] { "dependency" },
                    new string[] { "Dependency" }
                },

                new object[] {
                    new List<PackageType> {
                        new PackageType
                        {
                            Name = "DotNetCliTool"
                        },
                        new PackageType
                        {
                            Name = "Dependency"
                        }
                    },
                    new string[] { "dotnetclitool", "dependency" },
                    new string[] { "DotNetCliTool", "Dependency" },
                },

                new object[] {
                    new List<PackageType> {
                        new PackageType
                        {
                            Name = "DotNetCliTool",
                            Version = "1.0.0"
                        }
                    },
                    new string[] { "dotnetclitool" },
                    new string[] { "DotNetCliTool" }
                },
            };

            [Fact]
            public void AllSearchFiltersAreCovered()
            {
                var testedSearchFilters = AllSearchFilters.Select(x => (SearchFilters)x[0]).ToList();
                var allSearchFilters = Enum.GetValues(typeof(SearchFilters)).Cast<SearchFilters>().ToList();

                Assert.Empty(testedSearchFilters.Except(allSearchFilters));
                Assert.Empty(allSearchFilters.Except(testedSearchFilters));
            }

            public void SetDocumentLastUpdated(IUpdatedDocument document)
            {
                Data.SetDocumentLastUpdated(document, _output);
            }

            public BaseFacts(ITestOutputHelper output)
            {
                _output = output;
                _options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                _baseDocumentBuilder = new BaseDocumentBuilder(_options.Object); // We intentionally don't mock this.
                _config = new AzureSearchJobConfiguration
                {
                    GalleryBaseUrl = Data.GalleryBaseUrl,
                    FlatContainerBaseUrl = Data.FlatContainerBaseUrl,
                    FlatContainerContainerName = Data.FlatContainerContainerName,
                };

                _options.Setup(o => o.Value).Returns(() => _config);

                _target = new SearchDocumentBuilder(_baseDocumentBuilder);
            }

            public static IEnumerable<object[]> TargetFrameworkCases =>
                new List<object[]>
                {
                    new object[] {new List<string> {}, new List<string>(), new List<string> {}},
                    new object[] {new List<string> {"net"}, new List<string> {"net"}, new List<string> {"netframework"}},
                    new object[] {new List<string> {"win"}, new List<string> {"win"}, new List<string> {}},
                    new object[] {new List<string> {"dotnet"}, new List<string> {"dotnet"}, new List<string> {}},
                    new object[] {new List<string> {"net472"}, new List<string> {"net472"}, new List<string> {"netframework"}},
                    new object[] {new List<string> {"net40-client"}, new List<string> {"net40-client"}, new List<string> {"netframework"}},
                    new object[] {new List<string> {"net5.0"}, new List<string> {"net5.0"}, new List<string> {"net"}},
                    new object[] {new List<string> {"netcoreapp3.0"}, new List<string> {"netcoreapp3.0"}, new List<string> {"netcoreapp"}},
                    new object[] {new List<string> {"netstandard2.0"}, new List<string> {"netstandard2.0"}, new List<string> {"netstandard"}},
                    new object[] {new List<string> {"netstandard20", "netstandard21"}, new List<string> {"netstandard2.0", "netstandard2.1" },
                                    new List<string> {"netstandard"}},
                    new object[] {new List<string> {"net40", "net45"}, new List<string> {"net40", "net45"}, new List<string> {"netframework"}},
                    new object[] {new List<string> {"net5.0-tvos", "net5.0-ios"}, new List<string> {"net5.0-ios", "net5.0-tvos"}, 
                                    new List<string> {"net"}},
                    new object[] {new List<string> {"net5.1-tvos", "net5.1", "net5.0-tvos"},
                                    new List<string> {"net5.0-tvos", "net5.1", "net5.1-tvos"}, new List<string> {"net"}},
                    new object[] {new List<string> {"net5.0", "netcoreapp3.1", "native"}, new List<string> {"native", "net5.0", "netcoreapp3.1"},
                                    new List<string> {"net", "netcoreapp"}},
                    new object[] {new List<string> {"netcoreapp3.1", "netstandard2.0"}, new List<string> {"netcoreapp3.1", "netstandard2.0"},
                                    new List<string> {"netcoreapp", "netstandard"}},
                    new object[] {new List<string> {"netstandard2.1", "net45", "net472", "tizen40"},
                                    new List<string> {"netstandard2.1", "net45", "net472", "tizen40"},
                                    new List<string> {"netframework", "netstandard"}},
                    new object[] {new List<string>{"net40", "net471", "net5.0-watchos", "netstandard2.0", "netstandard2.1"},
                                    new List<string> {"net40", "net471", "net5.0-watchos", "netstandard2.0", "netstandard2.1"},
                                    new List<string>{"netframework", "net", "netstandard"}},
                    new object[] {new List<string>{"net45", "netstandard2.1", "xamarinios"},
                                    new List<string>{"net45", "netstandard2.1", "xamarinios"},
                                    new List<string> {"netframework", "netstandard"}},
                    new object[] {new List<string> {"net20", "net35", "net40", "net45", "netstandard1.0", "netstandard1.3", "netstandard2.0"},
                                    new List<string> {"net20", "net35", "net40", "net45", "netstandard1.0", "netstandard1.3", "netstandard2.0"},
                                    new List<string> {"netframework", "netstandard"}},
                    new object[] {new List<string> {"net6.0-android31.0"}, new List<string> {"net6.0-android"}, new List<string> {"net"}}, // normalize platform version
                    new object[] {new List<string> {"net5.0-tvos", "net5.0-ios13.0"}, new List<string> {"net5.0-ios", "net5.0-tvos"}, new List<string> {"net"}} // normalize platform version
                };

            public static IEnumerable<object[]> AdditionalPackageTFMCases =>
                new List<object[]>
                {
                        new object[] {new List<string> {"any"}, new List<string> {}, new List<string> {}},
                        new object[] {new List<string> {"foo"}, new List<string> {}, new List<string> {}} // unsupported tfm is not included
                };

            public static IEnumerable<object[]> AdditionalCatalogTFMCases =>
                new List<object[]>
                {
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(),
                                        new List<string> {"lib/netcoreapp31/_._", "lib/netstandard20/_._"},
                                        new List<string> {"netcoreapp3.1", "netstandard2.0"}, new List<string> {"netcoreapp", "netstandard"}},
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(), new List<string> {"lib/net40/_._", "lib/net4.7.1/_._"},
                                        new List<string> {"net40", "net471"}, new List<string> {"netframework"}},
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(), new List<string> {"lib/_._"},
                                        new List<string> {"net"}, new List<string> {"netframework"}}, // no version
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(),
                                        new List<string> {"runtimes/win/net40/_._", "runtimes/win/net471/_._"},
                                        new List<string>(), new List<string>()}, // no "lib" dir
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(),
                                        new List<string> {"runtimes/win/lib/net40/", "runtimes/win/lib/net471/_._"},
                                        new List<string> {"net471"}, new List<string> {"netframework"}}, // no file in "net40" dir
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(),
                                        new List<string> {"lib/net5.0/_1._", "lib/net5.0/_2._", "lib/native/_._"},
                                        new List<string> {"native", "net5.0" }, new List<string> {"net"}},
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(), new List<string> {"ref/_._"},
                                        new List<string>(), new List<string>()},
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(),
                                        new List<string> {"ref/net40/_._", "ref/net451/_._"},
                                        new List<string> {"net40", "net451"}, new List<string> {"netframework"}},
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(),
                                        new List<string> {"contentFiles/vb/net45/_._", "contentFiles/cs/netcoreapp3.1/_._"},
                                        new List<string>{"net45", "netcoreapp3.1"}, new List<string> {"netframework", "netcoreapp"}},

                        // Tools cases
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType> {new NuGet.Protocol.Catalog.PackageType{ Name = "DotnetTool" }},
                                        new List<string> {"tools/netcoreapp3.1/_._"}, new List<string>(), new List<string>()},
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType> {new NuGet.Protocol.Catalog.PackageType{ Name = "DotnetTool" }},
                                        new List<string> {"tools/netcoreapp3.1/win10-x86/tool1/_._", "tools/netcoreapp3.1/win10-x86/tool2/_._" },
                                        new List<string> {"netcoreapp3.1"}, new List<string> {"netcoreapp"}},
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType> {new NuGet.Protocol.Catalog.PackageType{ Name = "DotnetTool" }},
                                        new List<string> {"tools/netcoreapp3.1/any/_._"},
                                        new List<string> {"netcoreapp3.1"}, new List<string> {"netcoreapp"}},
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(), new List<string> {"tools/netcoreapp3.1/any/_._"},
                                        new List<string>(), new List<string>()}, // not a tools package, no supported TFMs
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType>(), // not a tools package
                                        new List<string> {"Foo.nuspec", "runtimes/win10-x86/lib/net40/_._", "runtimes/win10-x86/lib/net471/_._",
                                        "ref/net5.0-watchos/_1._", "ref/net5.0-watchos/_2._", "tools/netcoreapp3.1/win10-x86/tool1/_._",
                                        "tools/netcoreapp3.1/win10-x86/tool2/_._"},
                                        new List<string> {"net40", "net471", "net5.0-watchos"}, new List<string> {"netframework", "net"}},
                        new object[] {new List<NuGet.Protocol.Catalog.PackageType> {new NuGet.Protocol.Catalog.PackageType{ Name = "DotnetTool" }},
                                        new List<string> {"Foo.nuspec", "runtimes/win10-x86/lib/net40/_._", "runtimes/win10-x86/lib/net471/_._",
                                        "ref/net5.0-watchos/_1._", "ref/net5.0-watchos/_2._", "tools/netcoreapp3.1/win10-x86/tool1/_._",
                                        "tools/netcoreapp3.1/win10-x86/tool2/_._"},
                                        new List<string> {"netcoreapp3.1"}, new List<string> {"netcoreapp"}},
                };

            public static IEnumerable<object[]> ComputedFrameworkCases =>
                new List<object[]>
                {
                    new object[] {new List<string> {}, new List<string>(), new List<string> {}},
                    new object[] {new List<string> { "net5.0" }, new List<string> { "net5.0", "net6.0", "net7.0", "net5.0-windows" }, new List<string> { "net" }},
                    new object[] {new List<string> { "net6.0" }, new List<string> { "net6.0", "net7.0", "net6.0-android" }, new List<string> { "net" }},
                    new object[] {new List<string> { "net6.0-windows" }, new List<string> { "net6.0-windows", "net7.0-windows", "net8.0-windows" }, new List<string> { "net" }},
                    new object[] {new List<string> { "net462" }, new List<string> { "net462", "net472", "net481" }, new List<string> { "netframework" }},
                    new object[] {new List<string> { "netstandard2.1" }, new List<string> { "netstandard2.1", "net6.0", "net6.0-windows", "netcoreapp3.1", "tizen60" }, new List<string> { "net", "netstandard", "netcoreapp" }},
                    new object[] {new List<string> { "netcoreapp3.0" }, new List<string> { "netcoreapp3.0", "netcoreapp3.1", "net5.0", "net7.0-windows" }, new List<string> { "net", "netcoreapp" }},
                    new object[] {new List<string> { "net6.0-windows7.0" }, new List<string> { "net6.0-windows", "net7.0-windows", "net8.0-windows" }, new List<string> { "net" }}, // normalize platform version
                    new object[] {new List<string> { "net7.0-android99.9" }, new List<string> { "net7.0-android", "net8.0-android" }, new List<string> { "net" }}, // normalize platform version
                };
        }
    }
}

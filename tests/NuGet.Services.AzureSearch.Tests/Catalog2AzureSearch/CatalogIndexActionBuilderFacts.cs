// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;
using PackageDependency = NuGet.Protocol.Catalog.PackageDependency;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class CatalogIndexActionBuilderFacts
    {
        public class AddCatalogEntriesAsync : BaseFacts
        {
            public AddCatalogEntriesAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ThrowsWithNoEntries()
            {
                _latestEntries.Clear();

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf));
                Assert.Contains("There must be at least one catalog item to process.", ex.Message);
                Assert.Equal("latestEntries", ex.ParamName);
            }

            [Fact]
            public async Task AddFirstVersion()
            {
                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<SearchDocument.UpdateLatest>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.MergeOrUpload, x.ActionType));

                Assert.Single(indexActions.Hijack);
                Assert.IsType<HijackDocument.Full>(indexActions.Hijack[0].Document);
                Assert.Equal(IndexActionType.MergeOrUpload, indexActions.Hijack[0].ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { _packageVersion },
                    properties.Keys.ToArray());
                Assert.True(properties[_packageVersion].Listed);
                Assert.False(properties[_packageVersion].SemVer2);

                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()), Times.Once);
                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(_packageId), Times.Once);
            }

            [Fact]
            public async Task AddNewLatestVersion()
            {
                var existingVersion = "0.0.1";
                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        { existingVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                    }),
                    _versionListDataResult.AccessCondition);

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<SearchDocument.UpdateLatest>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.MergeOrUpload, x.ActionType));

                Assert.Equal(2, indexActions.Hijack.Count);
                var existing = indexActions.Hijack.Single(x => x.Document.Key == existingVersion);
                Assert.IsType<HijackDocument.Latest>(existing.Document);
                Assert.Equal(IndexActionType.Merge, existing.ActionType);
                var added = indexActions.Hijack.Single(x => x.Document.Key == _packageVersion);
                Assert.IsType<HijackDocument.Full>(added.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, added.ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { existingVersion, _packageVersion },
                    properties.Keys.ToArray());
                Assert.True(properties[existingVersion].Listed);
                Assert.False(properties[existingVersion].SemVer2);
                Assert.True(properties[_packageVersion].Listed);
                Assert.False(properties[_packageVersion].SemVer2);

                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()), Times.Once);
                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(_packageId), Times.Once);
            }

            [Fact]
            public async Task AddNewLatestVersionForOnlySomeSearchFilters()
            {
                var existingVersion = "0.0.1";
                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        { existingVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                    }),
                    _versionListDataResult.AccessCondition);
                SetVersion("1.0.0-beta");

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                var isPrerelease = indexActions.Search.ToLookup(x => x.Document.Key.Contains("Prerelease"));
                Assert.All(isPrerelease[false], x => Assert.IsType<SearchDocument.UpdateVersionListAndOwners>(x.Document));
                Assert.All(isPrerelease[false], x => Assert.Equal(IndexActionType.Merge, x.ActionType));
                Assert.All(isPrerelease[true], x => Assert.IsType<SearchDocument.UpdateLatest>(x.Document));
                Assert.All(isPrerelease[true], x => Assert.Equal(IndexActionType.MergeOrUpload, x.ActionType));

                Assert.Equal(2, indexActions.Hijack.Count);
                var existing = indexActions.Hijack.Single(x => x.Document.Key == existingVersion);
                Assert.IsType<HijackDocument.Latest>(existing.Document);
                Assert.Equal(IndexActionType.Merge, existing.ActionType);
                var added = indexActions.Hijack.Single(x => x.Document.Key == _packageVersion);
                Assert.IsType<HijackDocument.Full>(added.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, added.ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { existingVersion, _packageVersion },
                    properties.Keys.ToArray());
                Assert.True(properties[existingVersion].Listed);
                Assert.False(properties[existingVersion].SemVer2);
                Assert.True(properties[_packageVersion].Listed);
                Assert.False(properties[_packageVersion].SemVer2);

                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()), Times.Once);
                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(_packageId), Times.Once);
            }

            [Fact]
            public async Task AddNewNonLatestVersion()
            {
                var existingVersion = "1.0.1";
                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        { existingVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                    }),
                    _versionListDataResult.AccessCondition);

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<SearchDocument.UpdateVersionList>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.Merge, x.ActionType));

                Assert.Equal(2, indexActions.Hijack.Count);
                var existing = indexActions.Hijack.Single(x => x.Document.Key == existingVersion);
                Assert.IsType<HijackDocument.Latest>(existing.Document);
                Assert.Equal(IndexActionType.Merge, existing.ActionType);
                var added = indexActions.Hijack.Single(x => x.Document.Key == _packageVersion);
                Assert.IsType<HijackDocument.Full>(added.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, added.ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { _packageVersion, existingVersion },
                    properties.Keys.ToArray());
                Assert.True(properties[existingVersion].Listed);
                Assert.False(properties[existingVersion].SemVer2);
                Assert.True(properties[_packageVersion].Listed);
                Assert.False(properties[_packageVersion].SemVer2);

                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task Downgrade()
            {
                var existingVersion = "0.0.1";
                var existingLeaf = new PackageDetailsCatalogLeaf
                {
                    CommitTimestamp = new DateTimeOffset(2018, 12, 1, 0, 0, 0, TimeSpan.Zero),
                    Url = "http://example/leaf/0.0.1",
                    PackageId = _packageId,
                    VerbatimVersion = existingVersion,
                    PackageVersion = existingVersion,
                    Listed = true,
                };
                _leaf.Listed = false;
                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        { existingVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                        { _packageVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                    }),
                    _versionListDataResult.AccessCondition);
                _latestCatalogLeaves = new LatestCatalogLeaves(
                    new HashSet<NuGetVersion>(),
                    new Dictionary<NuGetVersion, PackageDetailsCatalogLeaf>
                    {
                        { NuGetVersion.Parse(existingVersion), existingLeaf },
                    });

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<SearchDocument.UpdateLatest>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.MergeOrUpload, x.ActionType));

                Assert.Equal(2, indexActions.Hijack.Count);
                var existing = indexActions.Hijack.Single(x => x.Document.Key == existingVersion);
                Assert.IsType<HijackDocument.Full>(existing.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, existing.ActionType);
                var added = indexActions.Hijack.Single(x => x.Document.Key == _packageVersion);
                Assert.IsType<HijackDocument.Full>(added.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, added.ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { existingVersion, _packageVersion },
                    properties.Keys.ToArray());
                Assert.True(properties[existingVersion].Listed);
                Assert.False(properties[existingVersion].SemVer2);
                Assert.False(properties[_packageVersion].Listed);
                Assert.False(properties[_packageVersion].SemVer2);

                _leafFetcher.Verify(
                    x => x.GetLatestLeavesAsync(
                        It.IsAny<string>(),
                        It.Is<IReadOnlyList<IReadOnlyList<NuGetVersion>>>(y => y.Count == 1)),
                    Times.Once);

                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()), Times.Once);
                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(_packageId), Times.Once);
            }

            [Fact]
            public async Task DowngradeToDifferent()
            {
                var existingVersion1 = "0.0.1";
                var existingVersion2 = "0.0.2-alpha";
                var existingLeaf1 = new PackageDetailsCatalogLeaf
                {
                    CommitTimestamp = new DateTimeOffset(2018, 12, 1, 0, 0, 0, TimeSpan.Zero),
                    Url = "http://example/leaf/0.0.1",
                    PackageId = _packageId,
                    VerbatimVersion = existingVersion1,
                    PackageVersion = existingVersion1,
                    Listed = true,
                };
                var existingLeaf2 = new PackageDetailsCatalogLeaf
                {
                    CommitTimestamp = new DateTimeOffset(2018, 12, 1, 0, 0, 0, TimeSpan.Zero),
                    Url = "http://example/leaf/0.0.2-alpha",
                    PackageId = _packageId,
                    VerbatimVersion = existingVersion2,
                    PackageVersion = existingVersion2,
                    Listed = true,
                    IsPrerelease = true,
                };
                _leaf.Listed = false;
                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        { existingVersion1, new VersionPropertiesData(listed: true, semVer2: false) },
                        { existingVersion2, new VersionPropertiesData(listed: true, semVer2: false) },
                        { _packageVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                    }),
                    _versionListDataResult.AccessCondition);
                _latestCatalogLeaves = new LatestCatalogLeaves(
                    new HashSet<NuGetVersion>(),
                    new Dictionary<NuGetVersion, PackageDetailsCatalogLeaf>
                    {
                        { NuGetVersion.Parse(existingVersion1), existingLeaf1 },
                        { NuGetVersion.Parse(existingVersion2), existingLeaf2 },
                    });

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<SearchDocument.UpdateLatest>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.MergeOrUpload, x.ActionType));

                Assert.Equal(3, indexActions.Hijack.Count);
                var existing1 = indexActions.Hijack.Single(x => x.Document.Key == existingVersion1);
                Assert.IsType<HijackDocument.Full>(existing1.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, existing1.ActionType);
                var existing2 = indexActions.Hijack.Single(x => x.Document.Key == existingVersion2);
                Assert.IsType<HijackDocument.Full>(existing2.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, existing2.ActionType);
                var added = indexActions.Hijack.Single(x => x.Document.Key == _packageVersion);
                Assert.IsType<HijackDocument.Full>(added.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, added.ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { existingVersion1, existingVersion2, _packageVersion },
                    properties.Keys.ToArray());
                Assert.True(properties[existingVersion1].Listed);
                Assert.False(properties[existingVersion1].SemVer2);
                Assert.True(properties[existingVersion2].Listed);
                Assert.False(properties[existingVersion2].SemVer2);
                Assert.False(properties[_packageVersion].Listed);
                Assert.False(properties[_packageVersion].SemVer2);

                _leafFetcher.Verify(
                    x => x.GetLatestLeavesAsync(
                        It.IsAny<string>(),
                        It.Is<IReadOnlyList<IReadOnlyList<NuGetVersion>>>(y => y.Count == 2)),
                    Times.Once);

                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()), Times.Once);
                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(_packageId), Times.Once);
            }

            [Fact]
            public async Task DowngradeToUnlist()
            {
                var existingVersion = "0.0.1";
                var existingLeaf = new PackageDetailsCatalogLeaf
                {
                    CommitTimestamp = new DateTimeOffset(2018, 12, 1, 0, 0, 0, TimeSpan.Zero),
                    Url = "http://example/leaf/0.0.1",
                    PackageId = _packageId,
                    VerbatimVersion = existingVersion,
                    PackageVersion = existingVersion,
                    Listed = false,
                };
                _leaf.Listed = false;
                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        { existingVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                        { _packageVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                    }),
                    _versionListDataResult.AccessCondition);
                _latestCatalogLeaves = new LatestCatalogLeaves(
                    new HashSet<NuGetVersion>(),
                    new Dictionary<NuGetVersion, PackageDetailsCatalogLeaf>
                    {
                        { NuGetVersion.Parse(existingVersion), existingLeaf },
                    });

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<KeyedDocument>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.Delete, x.ActionType));

                Assert.Equal(2, indexActions.Hijack.Count);
                var existing = indexActions.Hijack.Single(x => x.Document.Key == existingVersion);
                Assert.IsType<HijackDocument.Full>(existing.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, existing.ActionType);
                var added = indexActions.Hijack.Single(x => x.Document.Key == _packageVersion);
                Assert.IsType<HijackDocument.Full>(added.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, added.ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { existingVersion, _packageVersion },
                    properties.Keys.ToArray());
                Assert.False(properties[existingVersion].Listed);
                Assert.False(properties[existingVersion].SemVer2);
                Assert.False(properties[_packageVersion].Listed);
                Assert.False(properties[_packageVersion].SemVer2);

                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task DowngradeUnlistsOtherSearchFilterLatest()
            {
                var existingVersion1 = "2.5.11";
                var existingVersion2 = "3.0.107-pre";
                var existingVersion3 = "3.1.0+sha.8e3b68e";
                var existingLeaf1 = new PackageDetailsCatalogLeaf // This version is still listed.
                {
                    CommitTimestamp = new DateTimeOffset(2018, 12, 1, 0, 0, 0, TimeSpan.Zero),
                    Url = "http://example/leaf/1",
                    PackageId = _packageId,
                    VerbatimVersion = existingVersion1,
                    PackageVersion = existingVersion1,
                    Listed = true,
                };
                var existingLeaf2 = new PackageDetailsCatalogLeaf // This version is still listed.
                {
                    CommitTimestamp = new DateTimeOffset(2018, 12, 1, 0, 0, 0, TimeSpan.Zero),
                    Url = "http://example/leaf/2",
                    PackageId = _packageId,
                    VerbatimVersion = existingVersion2,
                    PackageVersion = existingVersion2,
                    Listed = true,
                };
                var newLeaf3 = new PackageDetailsCatalogLeaf // This version is no longer listed.
                {
                    CommitTimestamp = new DateTimeOffset(2018, 12, 1, 0, 0, 0, TimeSpan.Zero),
                    Url = "http://example/leaf/3",
                    PackageId = _packageId,
                    VerbatimVersion = existingVersion3,
                    PackageVersion = existingVersion3,
                    Listed = false,
                };

                SetVersion("3.2.0-dev.1+sha.ad6878e");
                _leaf.Listed = false;

                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        { existingVersion1, new VersionPropertiesData(listed: true, semVer2: false) },
                        { existingVersion2, new VersionPropertiesData(listed: true, semVer2: false) },
                        { existingVersion3, new VersionPropertiesData(listed: true, semVer2: true) },
                        { _packageVersion, new VersionPropertiesData(listed: true, semVer2: true) },
                    }),
                    _versionListDataResult.AccessCondition);
                _leafFetcher
                    .SetupSequence(x => x.GetLatestLeavesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<IReadOnlyList<NuGetVersion>>>()))
                    .ReturnsAsync(new LatestCatalogLeaves(
                        new HashSet<NuGetVersion>(),
                        new Dictionary<NuGetVersion, PackageDetailsCatalogLeaf>
                        {
                            { NuGetVersion.Parse(existingVersion2), existingLeaf2 },
                            { NuGetVersion.Parse(existingVersion3), newLeaf3 },
                        }))
                    .ReturnsAsync(new LatestCatalogLeaves(
                        new HashSet<NuGetVersion>(),
                        new Dictionary<NuGetVersion, PackageDetailsCatalogLeaf>
                        {
                            { NuGetVersion.Parse(existingVersion1), existingLeaf1 },
                        }))
                    .Throws<NotImplementedException>();

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<SearchDocument.UpdateLatest>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.MergeOrUpload, x.ActionType));

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { existingVersion1, existingVersion2, existingVersion3, _packageVersion },
                    properties.Keys.ToArray());
                Assert.True(properties[existingVersion1].Listed);
                Assert.False(properties[existingVersion1].SemVer2);
                Assert.True(properties[existingVersion2].Listed);
                Assert.False(properties[existingVersion2].SemVer2);
                Assert.False(properties[existingVersion3].Listed);
                Assert.True(properties[existingVersion3].SemVer2);
                Assert.False(properties[_packageVersion].Listed);
                Assert.True(properties[_packageVersion].SemVer2);

                _leafFetcher.Verify(
                    x => x.GetLatestLeavesAsync(_packageId, It.Is<IReadOnlyList<IReadOnlyList<NuGetVersion>>>(y =>
                        y.Count == 1 &&
                        y[0].Count == 3 &&
                        y[0][0] == NuGetVersion.Parse(existingVersion1) &&
                        y[0][1] == NuGetVersion.Parse(existingVersion2) &&
                        y[0][2] == NuGetVersion.Parse(existingVersion3))),
                    Times.Once);
                _leafFetcher.Verify(
                    x => x.GetLatestLeavesAsync(_packageId, It.Is<IReadOnlyList<IReadOnlyList<NuGetVersion>>>(y =>
                        y.Count == 1 &&
                        y[0].Count == 1 &&
                        y[0][0] == NuGetVersion.Parse(existingVersion1))),
                    Times.Once);
                _leafFetcher.Verify(
                    x => x.GetLatestLeavesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<IReadOnlyList<NuGetVersion>>>()),
                    Times.Exactly(2));

                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()), Times.Once);
                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(_packageId), Times.Once);
            }

            [Fact]
            public async Task DowngradeToDelete()
            {
                var existingVersion = "0.0.1";
                _leaf.Listed = false;
                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        { existingVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                        { _packageVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                    }),
                    _versionListDataResult.AccessCondition);
                _latestCatalogLeaves = new LatestCatalogLeaves(
                    new HashSet<NuGetVersion> { NuGetVersion.Parse(existingVersion) },
                    new Dictionary<NuGetVersion, PackageDetailsCatalogLeaf>());

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<KeyedDocument>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.Delete, x.ActionType));

                Assert.Equal(2, indexActions.Hijack.Count);
                var existing = indexActions.Hijack.Single(x => x.Document.Key == existingVersion);
                Assert.IsType<KeyedDocument>(existing.Document);
                Assert.Equal(IndexActionType.Delete, existing.ActionType);
                var added = indexActions.Hijack.Single(x => x.Document.Key == _packageVersion);
                Assert.IsType<HijackDocument.Full>(added.Document);
                Assert.Equal(IndexActionType.MergeOrUpload, added.ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { _packageVersion },
                    properties.Keys.ToArray());
                Assert.False(properties[_packageVersion].Listed);
                Assert.False(properties[_packageVersion].SemVer2);

                _ownerFetcher.Verify(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task DetectsSemVer2()
            {
                _leaf.DependencyGroups = new List<PackageDependencyGroup>
                {
                    new PackageDependencyGroup
                    {
                        Dependencies = new List<PackageDependency>
                        {
                            new PackageDependency
                            {
                                Range = "[1.0.0-alpha.1, )",
                            },
                        },
                    },
                };

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                var isSemVer2 = indexActions.Search.ToLookup(x => x.Document.Key.Contains("SemVer2"));
                Assert.All(isSemVer2[false], x => Assert.IsType<KeyedDocument>(x.Document));
                Assert.All(isSemVer2[false], x => Assert.Equal(IndexActionType.Delete, x.ActionType));
                Assert.All(isSemVer2[true], x => Assert.IsType<SearchDocument.UpdateLatest>(x.Document));
                Assert.All(isSemVer2[true], x => Assert.Equal(IndexActionType.MergeOrUpload, x.ActionType));

                Assert.Single(indexActions.Hijack);
                Assert.IsType<HijackDocument.Full>(indexActions.Hijack[0].Document);
                Assert.Equal(IndexActionType.MergeOrUpload, indexActions.Hijack[0].ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { _packageVersion },
                    properties.Keys.ToArray());
                Assert.True(properties[_packageVersion].Listed);
                Assert.True(properties[_packageVersion].SemVer2);
            }

            [Fact]
            public async Task DetectsUnlisted()
            {
                _leaf.Listed = false;

                var indexActions = await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                Assert.Equal(4, indexActions.Search.Count);
                Assert.All(indexActions.Search, x => Assert.IsType<KeyedDocument>(x.Document));
                Assert.All(indexActions.Search, x => Assert.Equal(IndexActionType.Delete, x.ActionType));

                Assert.Single(indexActions.Hijack);
                Assert.IsType<HijackDocument.Full>(indexActions.Hijack[0].Document);
                Assert.Equal(IndexActionType.MergeOrUpload, indexActions.Hijack[0].ActionType);

                Assert.Same(_versionListDataResult.AccessCondition, indexActions.VersionListDataResult.AccessCondition);
                var properties = indexActions.VersionListDataResult.Result.VersionProperties;
                Assert.Equal(
                    new[] { _packageVersion },
                    properties.Keys.ToArray());
                Assert.False(properties[_packageVersion].Listed);
                Assert.False(properties[_packageVersion].SemVer2);
            }

            [Fact]
            public async Task AssumesDateTimeIsUtc()
            {
                var existingVersion = "1.0.1";
                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>
                    {
                        { existingVersion, new VersionPropertiesData(listed: true, semVer2: false) },
                    }),
                    _versionListDataResult.AccessCondition);

                await _target.AddCatalogEntriesAsync(
                    _packageId,
                    _latestEntries,
                    _entryToLeaf);

                _search.Verify(
                    x => x.UpdateVersionListFromCatalog(
                        It.IsAny<string>(),
                        It.IsAny<SearchFilters>(),
                        new DateTimeOffset(_commitItem.CommitTimeStamp.Ticks, TimeSpan.Zero),
                        It.IsAny<string>(),
                        It.IsAny<string[]>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()),
                    Times.AtLeastOnce);

                _hijack.Verify(
                    x => x.LatestFromCatalog(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        new DateTimeOffset(_commitItem.CommitTimeStamp.Ticks, TimeSpan.Zero),
                        It.IsAny<string>(),
                        It.IsAny<HijackDocumentChanges>()),
                    Times.AtLeastOnce);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<IVersionListDataClient> _versionListDataClient;
            protected readonly Mock<ICatalogLeafFetcher> _leafFetcher;
            protected readonly Mock<IDatabaseOwnerFetcher> _ownerFetcher;
            protected readonly Mock<ISearchDocumentBuilder> _search;
            protected readonly Mock<IHijackDocumentBuilder> _hijack;
            protected readonly RecordingLogger<CatalogIndexActionBuilder> _logger;
            protected string _packageId;
            protected string _packageVersion;
            protected CatalogCommitItem _commitItem;
            protected PackageDetailsCatalogLeaf _leaf;
            protected readonly string[] _owners;
            protected ResultAndAccessCondition<VersionListData> _versionListDataResult;
            protected List<CatalogCommitItem> _latestEntries;
            protected Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> _entryToLeaf;
            protected LatestCatalogLeaves _latestCatalogLeaves;
            protected readonly CatalogIndexActionBuilder _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _versionListDataClient = new Mock<IVersionListDataClient>();
                _leafFetcher = new Mock<ICatalogLeafFetcher>();
                _ownerFetcher = new Mock<IDatabaseOwnerFetcher>();
                _search = new Mock<ISearchDocumentBuilder>();
                _hijack = new Mock<IHijackDocumentBuilder>();
                _logger = output.GetLogger<CatalogIndexActionBuilder>();

                _packageId = Data.PackageId;
                SetVersion("1.0.0");
                _versionListDataResult = new ResultAndAccessCondition<VersionListData>(
                    new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                    AccessConditionWrapper.GenerateIfNotExistsCondition());
                _owners = Data.Owners;
                _latestCatalogLeaves = new LatestCatalogLeaves(
                    new HashSet<NuGetVersion>(),
                    new Dictionary<NuGetVersion, PackageDetailsCatalogLeaf>());

                _versionListDataClient
                    .Setup(x => x.ReadAsync(It.IsAny<string>()))
                    .ReturnsAsync(() => _versionListDataResult);

                _search
                    .Setup(x => x.LatestFlagsOrNull(It.IsAny<VersionLists>(), It.IsAny<SearchFilters>()))
                    .Returns<VersionLists, SearchFilters>((vl, sf) => new SearchDocument.LatestFlags(
                        vl.GetLatestVersionInfoOrNull(sf),
                        isLatestStable: true,
                        isLatest: true));
                _search
                    .Setup(x => x.Keyed(It.IsAny<string>(), It.IsAny<SearchFilters>()))
                    .Returns<string, SearchFilters>(
                        (i, sf) => new KeyedDocument { Key = sf.ToString() });
                _search
                    .Setup(x => x.UpdateVersionListFromCatalog(
                        It.IsAny<string>(),
                        It.IsAny<SearchFilters>(),
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<string>(),
                        It.IsAny<string[]>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()))
                    .Returns<string, SearchFilters, DateTimeOffset, string, string[], bool, bool>(
                        (i, ct, ci, sf, v, ls, l) => new SearchDocument.UpdateVersionList { Key = sf.ToString() });
                _search
                    .Setup(x => x.UpdateVersionListAndOwnersFromCatalog(
                        It.IsAny<string>(),
                        It.IsAny<SearchFilters>(),
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<string>(),
                        It.IsAny<string[]>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<string[]>()))
                    .Returns<string, SearchFilters, DateTimeOffset, string, string[], bool, bool, string[]>(
                        (i, ct, ci, sf, v, ls, l, o) => new SearchDocument.UpdateVersionListAndOwners { Key = sf.ToString() });
                _search
                    .Setup(x => x.UpdateLatestFromCatalog(
                        It.IsAny<SearchFilters>(),
                        It.IsAny<string[]>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<PackageDetailsCatalogLeaf>(),
                        It.IsAny<string[]>()))
                    .Returns<SearchFilters, string[], bool, bool, string, string, PackageDetailsCatalogLeaf, string[]>(
                        (sf, v, ls, l, nv, fv, lf, o) => new SearchDocument.UpdateLatest { Key = sf.ToString() });

                _hijack
                    .Setup(x => x.Keyed(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns<string, string>(
                        (i, v) => new KeyedDocument { Key = v });
                _hijack
                    .Setup(x => x.LatestFromCatalog(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<string>(),
                        It.IsAny<HijackDocumentChanges>()))
                    .Returns<string, string, DateTimeOffset, string, HijackDocumentChanges>(
                        (i, v, ct, ci, c) => new HijackDocument.Latest { Key = v });
                _hijack
                    .Setup(x => x.FullFromCatalog(It.IsAny<string>(), It.IsAny<HijackDocumentChanges>(), It.IsAny<PackageDetailsCatalogLeaf>()))
                    .Returns<string, HijackDocumentChanges, PackageDetailsCatalogLeaf>(
                        (v, c, l) => new HijackDocument.Full { Key = v });

                _leafFetcher
                    .Setup(x => x.GetLatestLeavesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<IReadOnlyList<NuGetVersion>>>()))
                    .ReturnsAsync(() => _latestCatalogLeaves);

                _ownerFetcher
                    .Setup(x => x.GetOwnersOrEmptyAsync(It.IsAny<string>()))
                    .ReturnsAsync(() => _owners);

                _target = new CatalogIndexActionBuilder(
                    _versionListDataClient.Object,
                    _leafFetcher.Object,
                    _ownerFetcher.Object,
                    _search.Object,
                    _hijack.Object,
                    _logger);
            }

            protected void SetVersion(string version)
            {
                var parsedVersion = NuGetVersion.Parse(version);
                _packageVersion = version;
                _commitItem = new CatalogCommitItem(
                    new Uri("https://example/uri"),
                    "29e5c582-c1ef-4a5c-a053-d86c7381466b",
                    new DateTime(2018, 11, 1),
                    new List<string> { Schema.DataTypes.PackageDetails.AbsoluteUri },
                    new List<Uri> { Schema.DataTypes.PackageDetails },
                    new PackageIdentity(_packageId, parsedVersion));
                _leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = _packageId,
                    PackageVersion = _commitItem.PackageIdentity.Version.ToFullString(),
                    VerbatimVersion = _commitItem.PackageIdentity.Version.OriginalVersion,
                    IsPrerelease = parsedVersion.IsPrerelease,
                    Listed = true,
                };
                _latestEntries = new List<CatalogCommitItem> { _commitItem };
                _entryToLeaf = new Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>(
                    ReferenceEqualityComparer<CatalogCommitItem>.Default)
                {
                    { _commitItem, _leaf },
                };
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class VersionListsFacts
    {
        public class Constructor : BaseFacts
        {
            [Fact]
            public void CategorizesVersionsByFilterPredicate()
            {
                var list = Create(
                    _stableSemVer1Listed,
                    _prereleaseSemVer1Listed,
                    _stableSemVer2Listed,
                    _prereleaseSemVer2Listed);

                Assert.Equal(
                    new[]
                    {
                        SearchFilters.Default,
                        SearchFilters.IncludePrerelease,
                        SearchFilters.IncludeSemVer2,
                        SearchFilters.IncludePrereleaseAndSemVer2,
                    },
                    list._versionLists.Keys);
                Assert.Equal(
                    new[] { StableSemVer1 },
                    list._versionLists[SearchFilters.Default].FullVersions.ToArray());
                Assert.Equal(
                    new[] { StableSemVer1, PrereleaseSemVer1 },
                    list._versionLists[SearchFilters.IncludePrerelease].FullVersions.ToArray());
                Assert.Equal(
                    new[] { StableSemVer1, StableSemVer2 },
                    list._versionLists[SearchFilters.IncludeSemVer2].FullVersions.ToArray());
                Assert.Equal(
                    new[] { StableSemVer1, PrereleaseSemVer1, StableSemVer2, PrereleaseSemVer2 },
                    list._versionLists[SearchFilters.IncludePrereleaseAndSemVer2].FullVersions.ToArray());
            }

            [Fact]
            public void AllowsAllEmptyLists()
            {
                var list = Create();

                Assert.Equal(
                    new[]
                    {
                        SearchFilters.Default,
                        SearchFilters.IncludePrerelease,
                        SearchFilters.IncludeSemVer2,
                        SearchFilters.IncludePrereleaseAndSemVer2,
                    },
                    list._versionLists.Keys);
                Assert.Empty(list._versionLists[SearchFilters.Default]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludePrerelease]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludeSemVer2]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludePrereleaseAndSemVer2]._versions);
            }

            [Fact]
            public void AllowsSomeEmptyLists()
            {
                var list = Create(_prereleaseSemVer2Listed);

                Assert.Equal(
                    new[]
                    {
                        SearchFilters.Default,
                        SearchFilters.IncludePrerelease,
                        SearchFilters.IncludeSemVer2,
                        SearchFilters.IncludePrereleaseAndSemVer2,
                    },
                    list._versionLists.Keys);
                Assert.Empty(list._versionLists[SearchFilters.Default]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludePrerelease]._versions);
                Assert.Empty(list._versionLists[SearchFilters.IncludeSemVer2]._versions);
                Assert.Equal(
                    new[] { PrereleaseSemVer2 },
                    list._versionLists[SearchFilters.IncludePrereleaseAndSemVer2].FullVersions.ToArray());
            }
        }

        public class GetVersionListData : BaseFacts
        {
            [Fact]
            public void ReturnsCurrentSetOfVersions()
            {
                var list = new VersionLists(new VersionListData(new Dictionary<string, VersionPropertiesData>()));

                // add
                list.Upsert(_stableSemVer1Listed);

                // delete
                list.Upsert(_stableSemVer2Listed);
                list.Delete(_stableSemVer2Listed.FullVersion);

                // delete with different case
                list.Upsert(_prereleaseSemVer1Listed);
                list.Delete(_prereleaseSemVer1Listed.FullVersion.ToUpper());

                // unlist with different case
                list.Upsert(_prereleaseSemVer2Listed);
                list.Upsert(new VersionProperties(
                    _prereleaseSemVer2Listed.FullVersion.ToUpper(),
                    new VersionPropertiesData(listed: false, semVer2: true)));

                var data = list.GetVersionListData();
                Assert.Equal(
                    new[] { StableSemVer1, PrereleaseSemVer2.ToUpper() },
                    data.VersionProperties.Keys.ToArray());
            }
        }

        public class Upsert : BaseFacts
        {
            [Fact]
            public void ReplacesLatestFullVersionByNormalizedVersion()
            {
                var list = Create(
                    new VersionProperties("1.02.0-Alpha.1+git", new VersionPropertiesData(listed: true, semVer2: true)));

                list.Upsert("1.2.0.0-ALPHA.1+somethingelse", new VersionPropertiesData(listed: true, semVer2: true));

                Assert.Equal(
                    new[] { "1.2.0-ALPHA.1+somethingelse" },
                    list.GetVersionListData().VersionProperties.Keys.ToArray());
            }

            [Fact]
            public void ReplacesNonLatestFullVersionByNormalizedVersion()
            {
                var list = Create(
                    new VersionProperties("2.0.0", new VersionPropertiesData(listed: true, semVer2: true)),
                    new VersionProperties("1.02.0-Alpha.1+git", new VersionPropertiesData(listed: true, semVer2: true)));

                list.Upsert("1.2.0.0-ALPHA.1+somethingelse", new VersionPropertiesData(listed: true, semVer2: true));

                Assert.Equal(
                    new[] { "1.2.0-ALPHA.1+somethingelse", "2.0.0" },
                    list.GetVersionListData().VersionProperties.Keys.ToArray());
            }

            [Fact]
            public void DifferentUpdateLatestForAllFilters()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed, _stableSemVer2Listed, _prereleaseSemVer2Listed);
                var latest = new VersionProperties("5.0.0", new VersionPropertiesData(listed: true, semVer2: false));

                var output = list.Upsert(latest);
                
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[]
                    {
                        _stableSemVer1Listed.ParsedVersion,
                        _prereleaseSemVer1Listed.ParsedVersion,
                        _stableSemVer2Listed.ParsedVersion,
                        _prereleaseSemVer2Listed.ParsedVersion,
                        latest.ParsedVersion,
                    },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: false,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: false,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: false,
                        latestSemVer2: null),
                    output.Hijack[_stableSemVer2Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: false),
                    output.Hijack[_prereleaseSemVer2Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.Hijack[latest.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableLatestVersion()
            {
                var list = Create(_stableSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Listed);
                
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: true,
                        latestSemVer1: false,
                        latestStableSemVer2: true,
                        latestSemVer2: false),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: true,
                        latestStableSemVer2: false,
                        latestSemVer2: true),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableLatestUnlistedVersion()
            {
                var list = Create(_stableSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableNonLatestVersion()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Upsert(_stableSemVer1Listed);
                
                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: true,
                        latestSemVer1: false,
                        latestStableSemVer2: true,
                        latestSemVer2: false),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableNonLatestUnlistedVersion()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Upsert(_stableSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableLatestVersionWhenOnlyUnlistedExists()
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Upsert(_prereleaseSemVer1Listed);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: true,
                        latestStableSemVer2: false,
                        latestSemVer2: true),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableLatestUnlistedVersionWhenOnlyUnlistedExists()
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableNonLatestVersionWhenOnlyUnlistedExists()
            {
                var list = Create(_prereleaseSemVer1Unlisted);

                var output = list.Upsert(_stableSemVer1Listed);
                
                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void AddPartiallyApplicableNonLatestUnlistedVersionWhenOnlyUnlistedExists()
            {
                var list = Create(_prereleaseSemVer1Unlisted);

                var output = list.Upsert(_stableSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void UnlistLatestWhenOtherVersionExists()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void UnlistLatestWhenNoOtherVersionExists()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void UnlistLatestWhenOnlyUnlistOtherVersionExists()
            {
                var list = Create(_stableSemVer1Unlisted, _prereleaseSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void UnlistNonLatestWhenLatestExists()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed);

                var output = list.Upsert(_stableSemVer1Unlisted);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: true,
                        latestStableSemVer1: false,
                        latestSemVer1: false,
                        latestStableSemVer2: false,
                        latestSemVer2: false),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }
        }

        public class Delete : BaseFacts
        {
            [Fact]
            public void DeletesByNormalizedVersion()
            {
                var list = Create(
                    new VersionProperties("1.02.0-Alpha.1+git", new VersionPropertiesData(listed: true, semVer2: true)));

                list.Delete("1.2.0.0-ALPHA.1+somethingelse");

                Assert.Empty(list.GetVersionListData().VersionProperties);
            }

            [Fact]
            public void DeleteUnknownVersionWhenSingleListedVersionExists()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Delete(StableSemVer1);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void DeleteUnknownVersionWhenSingleUnlistedVersionExists()
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Delete(PrereleaseSemVer1);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void DeleteLatestVersionWhenSingleListedVersionExists()
            {
                var list = Create(_prereleaseSemVer1Listed);

                var output = list.Delete(PrereleaseSemVer1);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void DeleteLatestVersionWhenTwoListedVersionsExists()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed);

                var output = list.Delete(PrereleaseSemVer1);
                
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: true,
                        latestSemVer1: true,
                        latestStableSemVer2: true,
                        latestSemVer2: true),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }

            [Fact]
            public void DeleteNonLatestVersionWhenTwoListedVersionsExists()
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer1Listed);

                var output = list.Delete(StableSemVer1);
                
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.Default]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrerelease]);
                Assert.Equal(SearchIndexChangeType.Delete, output.Search[SearchFilters.IncludeSemVer2]);
                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search[SearchFilters.IncludePrereleaseAndSemVer2]);
                Assert.Equal(
                    new[] { _stableSemVer1Listed.ParsedVersion, _prereleaseSemVer1Listed.ParsedVersion },
                    output.Hijack.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: true,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: null,
                        latestStableSemVer2: null,
                        latestSemVer2: null),
                    output.Hijack[_stableSemVer1Listed.ParsedVersion]);
                Assert.Equal(
                    new MutableHijackIndexDocument(
                        delete: null,
                        updateMetadata: null,
                        latestStableSemVer1: null,
                        latestSemVer1: true,
                        latestStableSemVer2: null,
                        latestSemVer2: true),
                    output.Hijack[_prereleaseSemVer1Listed.ParsedVersion]);
            }
        }

        public abstract class BaseFacts
        {
            internal const string StableSemVer1 = "1.0.0";
            internal const string PrereleaseSemVer1 = "2.0.0-alpha";
            internal const string StableSemVer2 = "3.0.0";
            internal const string PrereleaseSemVer2 = "4.0.0-alpha";

            internal readonly VersionProperties _stableSemVer1Listed;
            internal readonly VersionProperties _stableSemVer1Unlisted;
            internal readonly VersionProperties _prereleaseSemVer1Listed;
            internal readonly VersionProperties _prereleaseSemVer1Unlisted;
            internal readonly VersionProperties _stableSemVer2Listed;
            internal readonly VersionProperties _stableSemVer2Unlisted;
            internal readonly VersionProperties _prereleaseSemVer2Listed;
            internal readonly VersionProperties _prereleaseSemVer2Unlisted;

            protected BaseFacts()
            {
                _stableSemVer1Listed = Create(StableSemVer1, true, false);
                _stableSemVer1Unlisted = Create(StableSemVer1, false, false);
                _prereleaseSemVer1Listed = Create(PrereleaseSemVer1, true, false);
                _prereleaseSemVer1Unlisted = Create(PrereleaseSemVer1, false, false);
                _stableSemVer2Listed = Create(StableSemVer2, true, true);
                _stableSemVer2Unlisted = Create(StableSemVer2, false, true);
                _prereleaseSemVer2Listed = Create(PrereleaseSemVer2, true, true);
                _prereleaseSemVer2Unlisted = Create(PrereleaseSemVer2, false, true);
            }

            private VersionProperties Create(string version, bool listed, bool semVer2)
            {
                return new VersionProperties(version, new VersionPropertiesData(listed, semVer2));
            }

            internal VersionLists Create(params VersionProperties[] versions)
            {
                var data = new VersionListData(versions.ToDictionary(x => x.FullVersion, x => x.Data));
                return new VersionLists(data);
            }
        }
    }
}

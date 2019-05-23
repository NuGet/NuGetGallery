// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class FilteredVersionListFacts
    {
        public class Upsert : BaseFacts
        {
            [Fact]
            public void ReplacesLatestFullVersionByNormalizedVersion()
            {
                ClearList();
                _list.Upsert(new FilteredVersionProperties("1.02.0-Alpha.1+git", listed: true));
                var properties = new FilteredVersionProperties("1.2.0.0-ALPHA.1+somethingelse", listed: true);

                var output = _list.Upsert(properties);

                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(properties.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(properties.ParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(properties.FullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(properties.ParsedVersion, _list._latestOrNull);
                Assert.Equal(properties.FullVersion, _list._latestOrNull.ToFullString());
                Assert.Equal(new[] { properties.FullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { properties.ParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void ReplacesNonLatestFullVersionByNormalizedVersion()
            {
                ClearList();
                var latest = new FilteredVersionProperties("2.0.0", listed: true);
                _list.Upsert(latest);
                _list.Upsert(new FilteredVersionProperties("1.02.0-Alpha.1+git", listed: true));
                var properties = new FilteredVersionProperties("1.2.0.0-ALPHA.1+somethingelse", listed: true);

                var output = _list.Upsert(properties);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(properties.ParsedVersion),
                        HijackIndexChange.SetLatestToFalse(properties.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(latest.ParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(latest.FullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(latest.ParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { properties.FullVersion, latest.FullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(
                    new[] { properties.ParsedVersion, latest.ParsedVersion },
                    _list._versions.Keys.ToArray());
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void AddingUnlistedVersion(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);

                var output = _list.Upsert(unlisted);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(unlisted.ParsedVersion),
                        HijackIndexChange.SetLatestToFalse(unlisted.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(
                    new[] { unlisted.ParsedVersion, InitialParsedVersion }.OrderBy(x => x).ToArray(),
                    _list._versions.Keys.ToArray());
            }

            [Fact]
            public void AddingVeryFirstVersion()
            {
                ClearList();

                var output = _list.Upsert(_initialVersionProperties);

                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void AddingFirstListedVersion(string version)
            {
                StartWithUnlisted();
                var listed = new FilteredVersionProperties(version, listed: true);

                var output = _list.Upsert(listed);

                Assert.Equal(SearchIndexChangeType.AddFirst, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(listed.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(listed.ParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(listed.FullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(listed.ParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { listed.FullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(
                    new[] { InitialParsedVersion, listed.ParsedVersion }.OrderBy(x => x).ToArray(),
                    _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UnlistingLatestAndNoOtherVersions()
            {
                var output = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void UnlistingLatestWithOtherUnlistedVersions(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);
                _list.Upsert(unlisted);

                var output = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Equal(
                    new[] { InitialParsedVersion, unlisted.ParsedVersion }.OrderBy(x => x).ToArray(),
                    _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UnlistingLatestWithOtherListedVersion()
            {
                var listed = new FilteredVersionProperties(PreviousFullVersion, listed: true);
                _list.Upsert(listed);

                var output = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                        HijackIndexChange.SetLatestToTrue(PreviousParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(listed.FullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(listed.ParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { listed.FullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { listed.ParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UnlistingNonLatestVersion()
            {
                var listed = new FilteredVersionProperties(NextFullVersion, listed: true);
                _list.Upsert(listed);

                var output = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                        HijackIndexChange.SetLatestToTrue(NextParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(listed.FullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(listed.ParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { listed.FullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion, listed.ParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void AddingListedNonLatestVersion()
            {
                var listed = new FilteredVersionProperties(PreviousFullVersion, listed: true);

                var output = _list.Upsert(listed);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(listed.ParsedVersion),
                        HijackIndexChange.SetLatestToFalse(listed.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { PreviousFullVersion, InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { listed.ParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void RelistingNonLatestVersion()
            {
                var listed = new FilteredVersionProperties(PreviousFullVersion, listed: true);
                _list.Upsert(listed);

                var output = _list.Upsert(listed);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(listed.ParsedVersion),
                        HijackIndexChange.SetLatestToFalse(listed.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { listed.FullVersion, InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { listed.ParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UnlistingUnlistedVersionAndNoOtherVersions()
            {
                StartWithUnlisted();

                var output = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UnlistingNewVersionAndNoOtherVersions()
            {
                ClearList();

                var output = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void RelistingLatestVersion()
            {
                var output = _list.Upsert(_initialVersionProperties);

                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void NewVersionIsIntroduced()
            {
                ClearList();
                var listed = new FilteredVersionProperties(PreviousFullVersion, listed: true);
                _list.Upsert(listed);

                var output = _list.Upsert(_initialVersionProperties);

                Assert.Equal(SearchIndexChangeType.UpdateLatest, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(PreviousParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { listed.FullVersion, InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { listed.ParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }
        }

        public class Remove : BaseFacts
        {
            [Fact]
            public void RemovesByNormalizedVersion()
            {
                ClearList();
                _list.Upsert(new FilteredVersionProperties("1.02.0-Alpha.1+git", listed: true));
                var removedVersion = NuGetVersion.Parse("1.02.0-Alpha.1+git");

                var output = _list.Remove(removedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(removedVersion),
                        HijackIndexChange.SetLatestToFalse(removedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list._versions.Keys);
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void RemovesNewVersion(string version)
            {
                var parsedVersion = NuGetVersion.Parse(version);

                var output = _list.Remove(parsedVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(parsedVersion),
                        HijackIndexChange.SetLatestToFalse(parsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void RemovesUnlistedVersion(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);
                _list.Upsert(unlisted);

                var output = _list.Remove(unlisted.ParsedVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(unlisted.ParsedVersion),
                        HijackIndexChange.SetLatestToFalse(unlisted.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void RemovesLatestVersion()
            {
                var latest = new FilteredVersionProperties(NextFullVersion, listed: true);
                _list.Upsert(latest);

                var output = _list.Remove(latest.ParsedVersion);

                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(latest.ParsedVersion),
                        HijackIndexChange.SetLatestToFalse(latest.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void RemovesVeryLastVersionWhenLastVersionIsListed()
            {
                StartWithUnlisted();

                var output = _list.Remove(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list._versions);
            }

            [Fact]
            public void RemovesVeryLastVersionWhenLastVersionIsUnlisted()
            {
                var output = _list.Remove(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list._versions);
            }

            [Fact]
            public void RemovesFromEmptyList()
            {
                ClearList();

                var output = _list.Remove(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list._versions.Keys);
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void RemovesLastListedVersion(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);
                _list.Upsert(unlisted);

                var output = _list.Remove(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Equal(new[] { unlisted.ParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void RemovesNonLatestListedVersionWithOneOtherVersion()
            {
                var nonLatest = new FilteredVersionProperties(PreviousFullVersion, listed: true);
                _list.Upsert(nonLatest);

                var output = _list.Remove(nonLatest.ParsedVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(nonLatest.ParsedVersion),
                        HijackIndexChange.SetLatestToFalse(nonLatest.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void RemovesNonLatestListedVersionWithTwoOtherVersions()
            {
                _list.Upsert(new FilteredVersionProperties(PreviousFullVersion, listed: true));
                _list.Upsert(new FilteredVersionProperties(NextFullVersion, listed: true));

                var output = _list.Remove(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(InitialParsedVersion),
                        HijackIndexChange.SetLatestToFalse(InitialParsedVersion),
                        HijackIndexChange.SetLatestToTrue(NextParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(NextFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(NextParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { PreviousFullVersion, NextFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { PreviousParsedVersion, NextParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void RemovesLatestListedVersionWithTwoOtherVersions()
            {
                _list.Upsert(new FilteredVersionProperties(PreviousFullVersion, listed: true));
                _list.Upsert(new FilteredVersionProperties(NextFullVersion, listed: true));

                var output = _list.Remove(NextParsedVersion);

                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.UpdateMetadata(NextParsedVersion),
                        HijackIndexChange.SetLatestToFalse(NextParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    Enumerable.ToArray(output.Hijack));
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { PreviousFullVersion, InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { PreviousParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }
        }

        public class Delete : BaseFacts
        {
            [Fact]
            public void DeletesByNormalizedVersion()
            {
                ClearList();
                _list.Upsert(new FilteredVersionProperties("1.02.0-Alpha.1+git", listed: true));
                var deletedVersion = NuGetVersion.Parse("1.02.0-Alpha.1+git");

                var output = _list.Delete(deletedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(deletedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list._versions.Keys);
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void DeletesNewVersion(string version)
            {
                var parsedVersion = NuGetVersion.Parse(version);

                var output = _list.Delete(parsedVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(parsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void DeletesUnlistedVersion(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);
                _list.Upsert(unlisted);

                var output = _list.Delete(unlisted.ParsedVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(unlisted.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DeletesLatestVersion()
            {
                var latest = new FilteredVersionProperties(NextFullVersion, listed: true);
                _list.Upsert(latest);

                var output = _list.Delete(latest.ParsedVersion);

                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(latest.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DeletesVeryLastVersionWhenLastVersionIsListed()
            {
                StartWithUnlisted();

                var output = _list.Delete(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list._versions);
            }

            [Fact]
            public void DeletesVeryLastVersionWhenLastVersionIsUnlisted()
            {
                var output = _list.Delete(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list._versions);
            }

            /// <summary>
            /// This behavior is important so that we can "reflow" deleted packages. Suppose there is a bug when all
            /// versions of a package are deleted but there is still a search document left over. We should be able
            /// to reflow a delete and force the document to be deleted.
            /// </summary>
            [Fact]
            public void DeletesFromEmptyList()
            {
                ClearList();

                var output = _list.Delete(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list._versions.Keys);
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void DeletesLastListedVersion(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);
                _list.Upsert(unlisted);

                var output = _list.Delete(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.Delete, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Null(_list.GetLatestVersionInfo());
                Assert.Null(_list._latestOrNull);
                Assert.Equal(new[] { unlisted.ParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DeletesNonLatestListedVersionWithOneOtherVersion()
            {
                var nonLatest = new FilteredVersionProperties(PreviousFullVersion, listed: true);
                _list.Upsert(nonLatest);

                var output = _list.Delete(nonLatest.ParsedVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(nonLatest.ParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DeletesNonLatestListedVersionWithTwoOtherVersions()
            {
                _list.Upsert(new FilteredVersionProperties(PreviousFullVersion, listed: true));
                _list.Upsert(new FilteredVersionProperties(NextFullVersion, listed: true));

                var output = _list.Delete(InitialParsedVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(InitialParsedVersion),
                        HijackIndexChange.SetLatestToTrue(NextParsedVersion),
                    },
                    output.Hijack.ToArray());
                Assert.Equal(NextFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(NextParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { PreviousFullVersion, NextFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { PreviousParsedVersion, NextParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DeletesLatestListedVersionWithTwoOtherVersions()
            {
                _list.Upsert(new FilteredVersionProperties(PreviousFullVersion, listed: true));
                _list.Upsert(new FilteredVersionProperties(NextFullVersion, listed: true));

                var output = _list.Delete(NextParsedVersion);

                Assert.Equal(SearchIndexChangeType.DowngradeLatest, output.Search);
                Assert.Equal(
                    new[]
                    {
                        HijackIndexChange.Delete(NextParsedVersion),
                        HijackIndexChange.SetLatestToTrue(InitialParsedVersion),
                    },
                    Enumerable.ToArray(output.Hijack));
                Assert.Equal(InitialFullVersion, _list.GetLatestVersionInfo().FullVersion);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { PreviousFullVersion, InitialFullVersion }, _list.GetLatestVersionInfo().ListedFullVersions);
                Assert.Equal(new[] { PreviousParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }
        }

        public class LatestOrNull
        {
            [Fact]
            public void UnlistedIsNotLatest()
            {
                var versions = new[]
                {
                    new FilteredVersionProperties("1.0.0", listed: true),
                    new FilteredVersionProperties("1.0.1", listed: false),
                };

                var list = new FilteredVersionList(versions);

                Assert.Equal("1.0.0", list.GetLatestVersionInfo().FullVersion);
            }

            [Fact]
            public void LatestIsNullForEmpty()
            {
                var versions = new FilteredVersionProperties[0];

                var list = new FilteredVersionList(versions);

                Assert.Null(list._latestOrNull);
            }

            [Fact]
            public void LatestIsNullForOnlyUnlisted()
            {
                var versions = new[]
                {
                    new FilteredVersionProperties("1.0.0", listed: false),
                    new FilteredVersionProperties("1.0.1", listed: false),
                };

                var list = new FilteredVersionList(versions);

                Assert.Null(list._latestOrNull);
            }
        }

        public class FullVersions
        {
            [Fact]
            public void ExcludesListedVersions()
            {
                var versions = new[]
                {
                    new FilteredVersionProperties("1.0.0", listed: true),
                    new FilteredVersionProperties("1.0.1", listed: false),
                };

                var list = new FilteredVersionList(versions);

                Assert.Equal(new[] { "1.0.0" }, list.GetLatestVersionInfo().ListedFullVersions);
            }

            [Fact]
            public void OrdersBySemVer()
            {
                var versions = new[]
                {
                    new FilteredVersionProperties("10.0.0", listed: true),
                    new FilteredVersionProperties("10.0.0-alpha", listed: true),
                    new FilteredVersionProperties("10.0.0-beta.10", listed: true),
                    new FilteredVersionProperties("10.0.0-beta.2", listed: true),
                    new FilteredVersionProperties("10.0.1", listed: true),
                    new FilteredVersionProperties("2.0.0", listed: true),
                };

                var list = new FilteredVersionList(versions);

                Assert.Equal(
                    new[] { "2.0.0", "10.0.0-alpha", "10.0.0-beta.2", "10.0.0-beta.10", "10.0.0", "10.0.1" },
                    list.GetLatestVersionInfo().ListedFullVersions);
            }
        }

        public abstract class BaseFacts
        {
            protected const string PreviousFullVersion = "0.9.0";
            protected const string InitialFullVersion = "1.0.0";
            protected const string NextFullVersion = "1.1.0";
            protected static readonly NuGetVersion PreviousParsedVersion = NuGetVersion.Parse(PreviousFullVersion);
            protected static readonly NuGetVersion InitialParsedVersion = NuGetVersion.Parse(InitialFullVersion);
            protected static readonly NuGetVersion NextParsedVersion = NuGetVersion.Parse(NextFullVersion);
            internal readonly FilteredVersionProperties _initialVersionProperties;
            internal readonly FilteredVersionProperties _unlistedVersionProperties;
            internal readonly FilteredVersionList _list;

            protected BaseFacts()
            {
                _initialVersionProperties = new FilteredVersionProperties(InitialFullVersion, listed: true);
                _unlistedVersionProperties = new FilteredVersionProperties(InitialFullVersion, listed: false);

                var versions = new[] { _initialVersionProperties };

                _list = new FilteredVersionList(versions);
            }

            protected void ClearList()
            {
                foreach (var version in _list._versions.ToList())
                {
                    _list.Delete(version.Value.ParsedVersion);
                }
            }

            protected void StartWithUnlisted()
            {
                ClearList();
                _list.Upsert(_unlistedVersionProperties);
            }

            public static IEnumerable<object[]> PreviousAndNextVersions => new[]
            {
                new object[] { PreviousFullVersion },
                new object[] { NextFullVersion },
            };
        }
    }
}

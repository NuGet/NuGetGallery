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

                var type = _list.Upsert(new FilteredVersionProperties("1.2.0.0-ALPHA.1+somethingelse", listed: true));

                Assert.Equal(SearchIndexChangeType.UpdateLatest, type);
                Assert.Equal(new[] { "1.2.0-ALPHA.1+somethingelse" }, _list.FullVersions.ToArray());
            }

            [Fact]
            public void ReplacesNonLatestFullVersionByNormalizedVersion()
            {
                ClearList();
                _list.Upsert(new FilteredVersionProperties("2.0.0", listed: true));
                _list.Upsert(new FilteredVersionProperties("1.02.0-Alpha.1+git", listed: true));

                var type = _list.Upsert(new FilteredVersionProperties("1.2.0.0-ALPHA.1+somethingelse", listed: true));

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, type);
                Assert.Equal(new[] { "1.2.0-ALPHA.1+somethingelse", "2.0.0" }, _list.FullVersions.ToArray());
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void UpdateVersionListForAddingUnlistedVersion(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);

                var type = _list.Upsert(unlisted);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(
                    new[] { unlisted.ParsedVersion, InitialParsedVersion }.OrderBy(x => x).ToArray(),
                    _list._versions.Keys.ToArray());
            }

            [Fact]
            public void AddFirstWhenAddingVeryFirstVersion()
            {
                ClearList();

                var type = _list.Upsert(_initialVersionProperties);

                Assert.Equal(SearchIndexChangeType.AddFirst, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void AddFirstWhenAddingFirstListedVersion(string version)
            {
                StartWithUnlisted();
                var listed = new FilteredVersionProperties(version, listed: true);

                var type = _list.Upsert(listed);

                Assert.Equal(SearchIndexChangeType.AddFirst, type);
                Assert.Equal(listed.FullVersion, _list.LatestOrNull);
                Assert.Equal(listed.ParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { listed.FullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(
                    new[] { InitialParsedVersion, listed.ParsedVersion }.OrderBy(x => x).ToArray(),
                    _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DeleteWhenUnlistingLatestAndNoOtherVersions()
            {
                var type = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.Delete, type);
                Assert.Null(_list.LatestOrNull);
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list.FullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void DeleteWhenUnlistingLatestWithOtherUnlistedVersions(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);
                _list.Upsert(unlisted);

                var type = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.Delete, type);
                Assert.Null(_list.LatestOrNull);
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list.FullVersions);
                Assert.Equal(
                    new[] { InitialParsedVersion, unlisted.ParsedVersion }.OrderBy(x => x).ToArray(),
                    _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DowngradeLatestWhenUnlistingLatestWithOtherListedVersion()
            {
                var listed = new FilteredVersionProperties(PreviousVersion, listed: true);
                _list.Upsert(listed);

                var type = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.DowngradeLatest, type);
                Assert.Equal(listed.FullVersion, _list.LatestOrNull);
                Assert.Equal(listed.ParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { listed.FullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { listed.ParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UpdateVersionListWhenUnlistingNonLatestVersion()
            {
                var listed = new FilteredVersionProperties(NextVersion, listed: true);
                _list.Upsert(listed);

                var type = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, type);
                Assert.Equal(listed.FullVersion, _list.LatestOrNull);
                Assert.Equal(listed.ParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { listed.FullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { InitialParsedVersion, listed.ParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UpdateVersionListWhenAddingListedNonLatestVersion()
            {
                var listed = new FilteredVersionProperties(PreviousVersion, listed: true);

                var type = _list.Upsert(listed);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { PreviousVersion, InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { listed.ParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UpdateVersionListWhenRelistingNonLatestVersion()
            {
                var listed = new FilteredVersionProperties(PreviousVersion, listed: true);
                _list.Upsert(listed);

                var type = _list.Upsert(listed);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { PreviousVersion, InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { listed.ParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DeleteWhenUnlistingUnlistedVersionAndNoOtherVersions()
            {
                StartWithUnlisted();

                var type = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.Delete, type);
                Assert.Null(_list.LatestOrNull);
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list.FullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DeleteWhenUnlistingNewVersionAndNoOtherVersions()
            {
                ClearList();

                var type = _list.Upsert(_unlistedVersionProperties);

                Assert.Equal(SearchIndexChangeType.Delete, type);
                Assert.Null(_list.LatestOrNull);
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list.FullVersions);
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UpdateExistingWhenRelistingLatestVersion()
            {
                var type = _list.Upsert(_initialVersionProperties);

                Assert.Equal(SearchIndexChangeType.UpdateLatest, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UpdateExistingNewVersionIsIntroduced()
            {
                ClearList();
                var listed = new FilteredVersionProperties(PreviousVersion, listed: true);
                _list.Upsert(listed);

                var type = _list.Upsert(_initialVersionProperties);

                Assert.Equal(SearchIndexChangeType.UpdateLatest, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { listed.FullVersion, InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { listed.ParsedVersion, InitialParsedVersion }, _list._versions.Keys.ToArray());
            }
        }

        public class Delete : BaseFacts
        {
            [Fact]
            public void DeletesByNormalizedVersion()
            {
                ClearList();
                _list.Upsert(new FilteredVersionProperties("1.02.0-Alpha.1+git", listed: true));

                var type = _list.Delete("1.2.0.0-ALPHA.1+somethingelse");

                Assert.Equal(SearchIndexChangeType.Delete, type);
                Assert.Empty(_list._versions);
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void UpdateVersionListForDeletingNewVersion(string version)
            {
                var type = _list.Delete(version);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void UpdateVersionListForDeletingUnlistedVersion(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);
                _list.Upsert(unlisted);

                var type = _list.Delete(unlisted.FullVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DowngradeForDeletingLatestVersion()
            {
                var latest = new FilteredVersionProperties(NextVersion, listed: true);
                _list.Upsert(latest);

                var type = _list.Delete(latest.FullVersion);

                Assert.Equal(SearchIndexChangeType.DowngradeLatest, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void DeleteForDeletingVeryLastVersionWhenLastVersionIsListed()
            {
                StartWithUnlisted();

                var type = _list.Delete(InitialFullVersion);

                Assert.Equal(SearchIndexChangeType.Delete, type);
                Assert.Null(_list.LatestOrNull);
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list.FullVersions);
                Assert.Empty(_list._versions);
            }

            [Fact]
            public void DeleteForDeletingVeryLastVersionWhenLastVersionIsUnlisted()
            {
                var type = _list.Delete(InitialFullVersion);

                Assert.Equal(SearchIndexChangeType.Delete, type);
                Assert.Null(_list.LatestOrNull);
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list.FullVersions);
                Assert.Empty(_list._versions);
            }

            /// <summary>
            /// This behavior is important so that we can "reflow" deleted packages. Suppose there is a bug when all
            /// versions of a package are deleted but there is still a search document left over. We should be able
            /// to reflow a delete and force the document to be deleted.
            /// </summary>
            [Fact]
            public void DeleteFromEmptyListIsDelete()
            {
                ClearList();

                var type = _list.Delete(InitialFullVersion);

                Assert.Equal(SearchIndexChangeType.Delete, type);
                Assert.Null(_list.LatestOrNull);
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list.FullVersions);
                Assert.Empty(_list._versions.Keys);
            }

            [Theory]
            [MemberData(nameof(PreviousAndNextVersions))]
            public void DeleteForDeletingLastListedVersion(string version)
            {
                var unlisted = new FilteredVersionProperties(version, listed: false);
                _list.Upsert(unlisted);

                var type = _list.Delete(InitialFullVersion);

                Assert.Equal(SearchIndexChangeType.Delete, type);
                Assert.Null(_list.LatestOrNull);
                Assert.Null(_list._latestOrNull);
                Assert.Empty(_list.FullVersions);
                Assert.Equal(new[] { unlisted.ParsedVersion }, _list._versions.Keys.ToArray());
            }

            [Fact]
            public void UpdateVersionListForDeletingNonLatestListedVersion()
            {
                var nonLatest = new FilteredVersionProperties(PreviousVersion, listed: true);
                _list.Upsert(nonLatest);

                var type = _list.Delete(nonLatest.FullVersion);

                Assert.Equal(SearchIndexChangeType.UpdateVersionList, type);
                Assert.Equal(InitialFullVersion, _list.LatestOrNull);
                Assert.Equal(InitialParsedVersion, _list._latestOrNull);
                Assert.Equal(new[] { InitialFullVersion }, _list.FullVersions.ToArray());
                Assert.Equal(new[] { InitialParsedVersion }, _list._versions.Keys.ToArray());
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

                Assert.Equal("1.0.0", list.LatestOrNull);
            }

            [Fact]
            public void LatestIsNullForEmpty()
            {
                var versions = new FilteredVersionProperties[0];

                var list = new FilteredVersionList(versions);

                Assert.Null(list.LatestOrNull);
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

                Assert.Null(list.LatestOrNull);
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

                Assert.Equal(new[] { "1.0.0" }, list.FullVersions.ToArray());
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
                    list.FullVersions.ToArray());
            }
        }

        public abstract class BaseFacts
        {
            protected const string PreviousVersion = "0.9.0";
            protected const string InitialFullVersion = "1.0.0";
            protected const string NextVersion = "1.1.0";
            protected static readonly NuGetVersion InitialParsedVersion = NuGetVersion.Parse(InitialFullVersion);
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
                    _list.Delete(version.Value.FullVersion);
                }
            }

            protected void StartWithUnlisted()
            {
                ClearList();
                _list.Upsert(_unlistedVersionProperties);
            }

            public static IEnumerable<object[]> PreviousAndNextVersions => new[]
            {
                new object[] { PreviousVersion },
                new object[] { NextVersion },
            };
        }
    }
}

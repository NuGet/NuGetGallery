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
                    new[] { "1.0.0" },
                    list._versionLists[SearchFilters.Default].FullVersions.ToArray());
                Assert.Equal(
                    new[] { "1.0.0-alpha", "1.0.0" },
                    list._versionLists[SearchFilters.IncludePrerelease].FullVersions.ToArray());
                Assert.Equal(
                    new[] { "1.0.0", "2.0.0" },
                    list._versionLists[SearchFilters.IncludeSemVer2].FullVersions.ToArray());
                Assert.Equal(
                    new[] { "1.0.0-alpha", "1.0.0", "2.0.0-alpha", "2.0.0" },
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
                    new[] { "2.0.0-alpha" },
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
                    new[] { "1.0.0", "2.0.0-ALPHA" },
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

                var type = list.Upsert("1.2.0.0-ALPHA.1+somethingelse", new VersionPropertiesData(listed: true, semVer2: true));

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

                var type = list.Upsert("1.2.0.0-ALPHA.1+somethingelse", new VersionPropertiesData(listed: true, semVer2: true));

                Assert.Equal(
                    new[] { "1.2.0-ALPHA.1+somethingelse", "2.0.0" },
                    list.GetVersionListData().VersionProperties.Keys.ToArray());
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.UpdateLatest)]
            public void AddListedVersionWhenSingleListedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer2Listed);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.UpdateVersionList)]
            public void AddUnlistedVersionWhenSingleListedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Listed);

                var output = list.Upsert(_prereleaseSemVer2Unlisted);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.AddFirst)]
            public void AddListedVersionWhenSingleUnlistedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Upsert(_prereleaseSemVer2Listed);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.Delete)]
            public void AddUnlistedVersionWhenSingleUnlistedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Upsert(_prereleaseSemVer2Unlisted);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.Delete)]
            public void UnlistLatestVersionWhenSingleListedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_prereleaseSemVer2Listed);

                var output = list.Upsert(_prereleaseSemVer2Unlisted);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.DowngradeLatest)]
            public void UnlistLatestVersionWhenOtherListedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer2Listed);

                var output = list.Upsert(_prereleaseSemVer2Unlisted);

                Assert.Equal(expected, output[searchFilters]);
            }
        }

        public class Delete : BaseFacts
        {
            [Fact]
            public void DeletesByNormalizedVersion()
            {
                var list = Create(
                    new VersionProperties("1.02.0-Alpha.1+git", new VersionPropertiesData(listed: true, semVer2: true)));

                var type = list.Delete("1.2.0.0-ALPHA.1+somethingelse");

                Assert.Empty(list.GetVersionListData().VersionProperties);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.UpdateVersionList)]
            public void DeleteListedVersionWhenSingleListedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Listed);

                var output = list.Delete(_prereleaseSemVer2Listed);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.Delete)]
            public void DeleteLatestVersionWhenSingleListedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_prereleaseSemVer2Listed);

                var output = list.Delete(_prereleaseSemVer2Listed);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.DowngradeLatest)]
            public void DeleteLatestVersionWhenTwoListedVersionsExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Listed, _prereleaseSemVer2Listed);

                var output = list.Delete(_prereleaseSemVer2Listed);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.UpdateVersionList)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.UpdateVersionList)]
            public void DeleteUnlistedVersionWhenSingleListedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Listed);

                var output = list.Delete(_prereleaseSemVer2Unlisted);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.Delete)]
            public void DeleteListedVersionWhenSingleUnlistedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Delete(_prereleaseSemVer2Listed);

                Assert.Equal(expected, output[searchFilters]);
            }

            [Theory]
            [InlineData(SearchFilters.Default, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrerelease, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludeSemVer2, SearchIndexChangeType.Delete)]
            [InlineData(SearchFilters.IncludePrereleaseAndSemVer2, SearchIndexChangeType.Delete)]
            public void DeleteUnlistedVersionWhenSingleUnlistedVersionExists(SearchFilters searchFilters, SearchIndexChangeType expected)
            {
                var list = Create(_stableSemVer1Unlisted);

                var output = list.Delete(_prereleaseSemVer2Unlisted);

                Assert.Equal(expected, output[searchFilters]);
            }
        }

        public abstract class BaseFacts
        {
            internal const string StableSemVer1 = "1.0.0";
            internal const string PrereleaseSemVer1 = "1.0.0-alpha";
            internal const string StableSemVer2 = "2.0.0";
            internal const string PrereleaseSemVer2 = "2.0.0-alpha";

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

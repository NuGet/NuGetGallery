// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// A container for all versions lists. There is one version list tracked per combination of
    /// <see cref="SearchFilters"/> flags.
    /// </summary>
    public class VersionLists
    {
        private static readonly IReadOnlyDictionary<SearchFilters, Func<VersionProperties, bool>> SearchFilterPredicates
            = new Dictionary<SearchFilters, Func<VersionProperties, bool>>
            {
                {
                    SearchFilters.Default,
                    p => !p.ParsedVersion.IsPrerelease && !p.Data.SemVer2
                },
                {
                    SearchFilters.IncludePrerelease,
                    p => !p.Data.SemVer2
                },
                {
                    SearchFilters.IncludeSemVer2,
                    p => !p.ParsedVersion.IsPrerelease
                },
                {
                    SearchFilters.IncludePrereleaseAndSemVer2,
                    p => true
                },
            };

        private static readonly IReadOnlyList<SearchFilters> AllSearchFilters = SearchFilterPredicates
            .Select(x => x.Key)
            .ToList();

        internal readonly Dictionary<SearchFilters, FilteredVersionList> _versionLists;
        internal readonly SortedDictionary<NuGetVersion, KeyValuePair<string, VersionPropertiesData>> _versionProperties;

        public VersionLists(VersionListData data)
        {
            var allVersions = data
                .VersionProperties
                .Select(p => new VersionProperties(p.Key, p.Value))
                .OrderBy(x => x.ParsedVersion)
                .ToList();

            _versionProperties = new SortedDictionary<NuGetVersion, KeyValuePair<string, VersionPropertiesData>>();
            foreach (var version in allVersions)
            {
                _versionProperties.Add(version.ParsedVersion, KeyValuePair.Create(version.FullVersion, version.Data));
            }

            _versionLists = new Dictionary<SearchFilters, FilteredVersionList>();
            foreach (var pair in SearchFilterPredicates)
            {
                var searchFilter = pair.Key;
                var predicate = pair.Value;
                var listState = new FilteredVersionList(allVersions
                    .Where(predicate)
                    .Select(x => x.Filtered));
                _versionLists.Add(searchFilter, listState);
            }
        }

        public VersionListData GetVersionListData()
        {
            return new VersionListData(_versionProperties.Values.ToDictionary(x => x.Key, x => x.Value));
        }

        public IReadOnlyDictionary<SearchFilters, SearchIndexChangeType> Upsert(
            string fullOrOriginalVersion,
            VersionPropertiesData data)
        {
            var added = new VersionProperties(fullOrOriginalVersion, data);
            _versionProperties[added.ParsedVersion] = KeyValuePair.Create(added.FullVersion, data);

            var output = new Dictionary<SearchFilters, SearchIndexChangeType>();
            foreach (var pair in _versionLists)
            {
                var searchFilter = pair.Key;
                var listState = pair.Value;
                var predicate = SearchFilterPredicates[searchFilter];

                SearchIndexChangeType changeType;
                if (predicate(added))
                {
                    changeType = listState.Upsert(added.Filtered);
                }
                else
                {
                    changeType = listState.Remove(added.ParsedVersion);
                }

                output[searchFilter] = changeType;
            }

            return output;
        }

        public IReadOnlyDictionary<SearchFilters, SearchIndexChangeType> Delete(string version)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            _versionProperties.Remove(parsedVersion);

            var output = new Dictionary<SearchFilters, SearchIndexChangeType>();
            foreach (var pair in _versionLists)
            {
                var searchFilter = pair.Key;
                var listState = pair.Value;

                // We can execute this on all lists, no matter the search filter predicate because removing a version
                // that was never added will result in recalculating the version list, which supports the reflow
                // scenario.
                output[searchFilter] = listState.Delete(parsedVersion);
            }

            return output;
        }
    }
}

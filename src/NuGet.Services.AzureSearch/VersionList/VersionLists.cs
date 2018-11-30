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

        public LatestVersionInfo GetLatestVersionInfoOrNull(SearchFilters searchFilters)
        {
            if (!_versionLists.TryGetValue(searchFilters, out var listState))
            {
                return null;
            }

            return listState.GetLatestVersionInfo();
        }

        public VersionListData GetVersionListData()
        {
            return new VersionListData(_versionProperties.Values.ToDictionary(x => x.Key, x => x.Value));
        }

        public IndexChanges ApplyChanges(IEnumerable<VersionListChange> changes)
        {
            return ApplyChangesInternal(changes).Solidify();
        }

        internal MutableIndexChanges ApplyChangesInternal(IEnumerable<VersionListChange> changes)
        {
            var mutableChanges = new MutableIndexChanges();

            // Process the changes in descending order.
            var sortedChanges = changes
                .OrderByDescending(x => x.ParsedVersion)
                .ToList();

            // Verify that there is only one change per version.
            var versionsWithMultipleChanges = sortedChanges
                .GroupBy(x => x.ParsedVersion)
                .OrderBy(x => x.Key)
                .Select(x => KeyValuePair.Create(x.Key.ToFullString(), x.Count()))
                .Where(x => x.Value > 1)
                .ToList();
            if (versionsWithMultipleChanges.Any())
            {
                throw new ArgumentException(
                    $"There are multiple changes for the following version(s): " +
                    string.Join(", ", versionsWithMultipleChanges.Select(x => $"{x.Key} ({x.Value} changes)")),
                    nameof(changes));
            }

            foreach (var change in sortedChanges)
            {
                MutableIndexChanges versionChanges;
                if (!change.IsDelete)
                {
                    versionChanges = Upsert(change.FullVersion, change.ParsedVersion, change.Data);
                }
                else
                {
                    versionChanges = Delete(change.ParsedVersion);
                }

                mutableChanges.Merge(versionChanges);
            }

            // Verify that we are updating the metadata of all non-delete changes.
            foreach (var change in changes)
            {
                Guard.Assert(
                    mutableChanges.HijackDocuments.ContainsKey(change.ParsedVersion),
                    $"The should be a hijack document for each changed version. Version {change.FullVersion} does " +
                    "not have a hijack document.");
                
                if (!change.IsDelete)
                {
                    Guard.Assert(
                        mutableChanges.HijackDocuments[change.ParsedVersion].UpdateMetadata == true,
                        $"The metadata of version {change.FullVersion} should be updated.");
                }
            }

            return mutableChanges;
        }

        internal MutableIndexChanges Upsert(string fullOrOriginalVersion, VersionPropertiesData data)
        {
            var parsedVersion = NuGetVersion.Parse(fullOrOriginalVersion);
            return Upsert(parsedVersion.ToFullString(), parsedVersion, data);
        }

        private MutableIndexChanges Upsert(
            string fullVersion,
            NuGetVersion parsedVersion,
            VersionPropertiesData data)
        {
            var added = new VersionProperties(fullVersion, parsedVersion, data);
            _versionProperties[added.ParsedVersion] = KeyValuePair.Create(added.FullVersion, data);

            // Detect changes related to the latest versions, per search filter.
            var output = new Dictionary<SearchFilters, LatestIndexChanges>();
            foreach (var pair in _versionLists)
            {
                var searchFilter = pair.Key;
                var listState = pair.Value;
                var predicate = SearchFilterPredicates[searchFilter];

                LatestIndexChanges latestIndexChanges;
                if (predicate(added))
                {
                    latestIndexChanges = listState.Upsert(added.Filtered);
                }
                else
                {
                    latestIndexChanges = listState.Remove(added.ParsedVersion);
                }

                output[searchFilter] = latestIndexChanges;
            }

            return MutableIndexChanges.FromLatestIndexChanges(output);
        }

        internal MutableIndexChanges Delete(NuGetVersion parsedVersion)
        {
            _versionProperties.Remove(parsedVersion);

            // Detect changes related to the latest versions, per search filter.
            var output = new Dictionary<SearchFilters, LatestIndexChanges>();
            foreach (var pair in _versionLists)
            {
                var searchFilter = pair.Key;
                var listState = pair.Value;

                // We can execute this on all lists, no matter the search filter predicate because removing a version
                // that was never added will result in recalculating the version list, which supports the reflow
                // scenario.
                output[searchFilter] = listState.Delete(parsedVersion);
            }

            return MutableIndexChanges.FromLatestIndexChanges(output);
        }
    }
}

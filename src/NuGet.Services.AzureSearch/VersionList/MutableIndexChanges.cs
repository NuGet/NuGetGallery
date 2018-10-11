// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// A mutable version of <see cref="IndexChanges"/>.
    /// </summary>
    internal class MutableIndexChanges
    {
        public MutableIndexChanges()
        {
            Search = new Dictionary<SearchFilters, SearchIndexChangeType>();
            Hijack = new Dictionary<NuGetVersion, MutableHijackIndexDocument>();
        }

        public MutableIndexChanges(
            Dictionary<SearchFilters, SearchIndexChangeType> search,
            Dictionary<NuGetVersion, MutableHijackIndexDocument> hijack)
        {
            Search = search ?? throw new ArgumentNullException(nameof(search));
            Hijack = hijack ?? throw new ArgumentNullException(nameof(hijack));
        }

        public Dictionary<SearchFilters, SearchIndexChangeType> Search { get; }
        public Dictionary<NuGetVersion, MutableHijackIndexDocument> Hijack { get; }

        public static MutableIndexChanges FromLatestIndexChanges(
            IReadOnlyDictionary<SearchFilters, LatestIndexChanges> latestIndexChanges)
        {
            // Take the search index changes as-is.
            var search = latestIndexChanges.ToDictionary(x => x.Key, x => x.Value.Search);

            // Group hijack index changes by version.
            var versionGroups = latestIndexChanges
                .SelectMany(pair => pair
                    .Value
                    .Hijack
                    .Select(change => new { SearchFilters = pair.Key, change.Type, change.Version }))
                .GroupBy(x => x.Version);

            // Apply all of the changes related to each version.
            var hijack = new Dictionary<NuGetVersion, MutableHijackIndexDocument>();
            foreach (var group in versionGroups)
            {
                var document = new MutableHijackIndexDocument();
                foreach (var change in group)
                {
                    document.ApplyChange(change.SearchFilters, change.Type);
                }

                hijack.Add(group.Key, document);
            }

            // Verify that there are not multiple versions set to latest, per search filter.
            foreach (var searchFilters in latestIndexChanges.Keys)
            {
                var latestVersions = hijack
                    .Where(x => x.Value.GetLatest(searchFilters).GetValueOrDefault(false))
                    .Select(x => x.Key.ToFullString())
                    .ToList();

                Guard.Assert(
                    latestVersions.Count <= 1,
                    $"There are multiple latest versions for search filters '{searchFilters}': {string.Join(", ", latestVersions)}");
            }

            return new MutableIndexChanges(search, hijack);
        }
    }
}

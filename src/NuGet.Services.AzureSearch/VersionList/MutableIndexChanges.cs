// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;
using SICT = NuGet.Services.AzureSearch.SearchIndexChangeType;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// A mutable version of <see cref="IndexChanges"/>.
    /// </summary>
    internal class MutableIndexChanges
    {
        internal static readonly IReadOnlyList<SearchFilters> AllSearchFilters = Enum
            .GetValues(typeof(SearchFilters))
            .Cast<SearchFilters>()
            .ToList();

        /// <summary>
        /// This is a dictionary where the key is the state transition. The value of the dictionary is the resulting
        /// state from that transition. Remember that versions are processed in descending version order so some state
        /// transition should not be possible.
        /// </summary>
        private static readonly IReadOnlyDictionary<StateTransition, SICT> AcceptableTransitions
            = new Dictionary<StateTransition, SICT>
            {
                // Example: add an initial, listed version then add a lower, listed version
                { new StateTransition(SICT.AddFirst, SICT.UpdateVersionList), SICT.AddFirst },

                // Example: add an initial, unlisted version then add a lower, listed version
                { new StateTransition(SICT.Delete, SICT.AddFirst), SICT.AddFirst },

                // Example: add a new latest, listed version then add a lower, listed version
                { new StateTransition(SICT.UpdateLatest, SICT.UpdateVersionList), SICT.UpdateLatest },  

                // Example: delete a non-existent version then unlist the latest version
                { new StateTransition(SICT.UpdateVersionList, SICT.Delete), SICT.Delete },

                // Example: unlist an already unlisted higher version then unlist the latest version
                { new StateTransition(SICT.UpdateVersionList, SICT.DowngradeLatest), SICT.DowngradeLatest },

                // Example: unlist an already unlisted higher version then add a new latest version
                { new StateTransition(SICT.UpdateVersionList, SICT.UpdateLatest), SICT.UpdateLatest },

                // Example: unlist the latest version then add a new latest version
                { new StateTransition(SICT.DowngradeLatest, SICT.UpdateLatest), SICT.UpdateLatest },

                // Example: unlist the latest version then add a new non-latest version
                { new StateTransition(SICT.DowngradeLatest, SICT.UpdateVersionList), SICT.DowngradeLatest },

                // Example: delete the latest version then delete the last latest version
                { new StateTransition(SICT.DowngradeLatest, SICT.Delete), SICT.Delete },
            };

        public MutableIndexChanges()
        {
            SearchChanges = new Dictionary<SearchFilters, SICT>();
            HijackChanges = new Dictionary<NuGetVersion, List<KeyValuePair<SearchFilters, HijackIndexChangeType>>>();
            HijackDocuments = new Dictionary<NuGetVersion, MutableHijackDocumentChanges>();
        }

        public MutableIndexChanges(
            Dictionary<SearchFilters, SICT> search,
            Dictionary<NuGetVersion, List<KeyValuePair<SearchFilters, HijackIndexChangeType>>> hijack)
        {
            SearchChanges = search ?? throw new ArgumentNullException(nameof(search));
            HijackChanges = hijack ?? throw new ArgumentNullException(nameof(hijack));
            HijackDocuments = hijack.ToDictionary(
                x => x.Key,
                x => InitializeHijackDocumentChanges(x.Value));
        }

        private static MutableHijackDocumentChanges InitializeHijackDocumentChanges(
            IEnumerable<KeyValuePair<SearchFilters, HijackIndexChangeType>> changes)
        {
            var document = new MutableHijackDocumentChanges();
            foreach (var change in changes)
            {
                document.ApplyChange(change.Key, change.Value);
            }

            return document;
        }

        public Dictionary<SearchFilters, SICT> SearchChanges { get; }
        private Dictionary<NuGetVersion, List<KeyValuePair<SearchFilters, HijackIndexChangeType>>> HijackChanges { get; }

        /// <summary>
        /// Keep track of the hijack document as we merge multiple <see cref="MutableIndexChanges"/>. This allows
        /// us to detect consistency problems are quickly as possible.
        /// </summary>
        public Dictionary<NuGetVersion, MutableHijackDocumentChanges> HijackDocuments { get; }

        public static MutableIndexChanges FromLatestIndexChanges(
            IReadOnlyDictionary<SearchFilters, LatestIndexChanges> latestIndexChanges)
        {
            // Take the search index changes as-is.
            var search = latestIndexChanges.ToDictionary(x => x.Key, x => x.Value.Search);

            // Group hijack index changes by version.
            var hijack = latestIndexChanges
                .SelectMany(pair => pair
                    .Value
                    .Hijack
                    .Select(change => new { SearchFilters = pair.Key, change.Type, change.Version }))
                .GroupBy(x => x.Version)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(y => KeyValuePair.Create(y.SearchFilters, y.Type)).ToList());

            return new MutableIndexChanges(search, hijack);
        }

        public void Merge(MutableIndexChanges added)
        {
            if (added == null)
            {
                throw new ArgumentNullException(nameof(added));
            }

            foreach (var pair in added.SearchChanges)
            {
                MergeSearchIndexChanges(pair.Key, pair.Value);
            }

            foreach (var pair in added.HijackChanges)
            {
                MergeHijackIndexChanges(pair.Key, pair.Value);
            }

            // Verify that there are not multiple latest versions per search filter.
            foreach (var searchFilters in AllSearchFilters)
            {
                var latest = HijackDocuments
                    .Where(x => x.Value.GetLatest(searchFilters).GetValueOrDefault(false))
                    .Select(x => x.Key.ToFullString())
                    .ToList();
                Guard.Assert(
                    latest.Count <= 1,
                    $"There are {latest.Count} versions set to be latest on search filter {searchFilters}: {string.Join(", ", latest)}");
            }
        }

        private void MergeSearchIndexChanges(SearchFilters searchFilters, SICT addedType)
        {
            if (!SearchChanges.TryGetValue(searchFilters, out var existingType))
            {
                SearchChanges[searchFilters] = addedType;
                return;
            }

            // If the search index change type is the same, move on.
            if (existingType == addedType)
            {
                return;
            }

            var transition = new StateTransition(existingType, addedType);
            if (AcceptableTransitions.TryGetValue(transition, out var result))
            {
                SearchChanges[searchFilters] = result;
                return;
            }

            Guard.Fail($"A {existingType} search index change cannot be replaced with {addedType}.");
        }

        private void MergeHijackIndexChanges(
            NuGetVersion version,
            List<KeyValuePair<SearchFilters, HijackIndexChangeType>> addedChanges)
        {
            // If the version does not yet exist, add it and move on.
            if (!HijackChanges.TryGetValue(version, out var existingChanges))
            {
                HijackDocuments.Add(version, InitializeHijackDocumentChanges(addedChanges));
                HijackChanges.Add(version, addedChanges);
            }
            else
            {
                var document = HijackDocuments[version];
                foreach (var change in addedChanges)
                {
                    document.ApplyChange(change.Key, change.Value);
                }

                existingChanges.AddRange(addedChanges);
            }
        }

        public IndexChanges Solidify()
        {
            // Verify that the running list of hijack changes is the same as the pre-computed hijack document.
            Guard.Assert(HijackChanges.Count == HijackDocuments.Count, "The hijack document state has diverged.");
            foreach (var pair in HijackChanges)
            {
                var expected = InitializeHijackDocumentChanges(pair.Value);
                var actual = HijackDocuments[pair.Key];
                Guard.Assert(
                    expected == actual,
                    $"The hijack document for {pair.Key.ToFullString()} is different than the list of index changes.");
            }

            return new IndexChanges(
                SearchChanges.ToDictionary(x => x.Key, x => x.Value),
                HijackDocuments.ToDictionary(x => x.Key, x => x.Value.Solidify()));
        }

        private class StateTransition : IEquatable<StateTransition>
        {
            public StateTransition(SICT existing, SICT added)
            {
                Existing = existing;
                Added = added;
            }

            public SICT Existing { get; }
            public SICT Added { get; }

            public override bool Equals(object obj)
            {
                return Equals(obj as StateTransition);
            }

            public bool Equals(StateTransition transition)
            {
                return transition != null &&
                       Existing == transition.Existing &&
                       Added == transition.Added;
            }

            /// <summary>
            /// This method was generated by Visual Studio.
            /// </summary>
            public override int GetHashCode()
            {
                var hashCode = -699697695;
                hashCode = hashCode * -1521134295 + Existing.GetHashCode();
                hashCode = hashCode * -1521134295 + Added.GetHashCode();
                return hashCode;
            }
        }
    }
}

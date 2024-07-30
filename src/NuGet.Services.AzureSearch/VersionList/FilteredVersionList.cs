// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// This type tracks a version list for a specific <see cref="SearchFilters"/>. In other words, this implementation
    /// assumes that a non-applicable version is never added to the list. In practice, <see cref="VersionLists"/> does
    /// this filtering before adding a version to this list.
    /// </summary>
    internal class FilteredVersionList
    {
        internal readonly SortedList<NuGetVersion, FilteredVersionProperties> _versions;
        internal NuGetVersion _latestOrNull;

        public FilteredVersionList(IEnumerable<FilteredVersionProperties> versions)
        {
            if (versions == null)
            {
                throw new ArgumentNullException(nameof(versions));
            }

            _versions = new SortedList<NuGetVersion, FilteredVersionProperties>();

            foreach (var version in versions)
            {
                _versions[version.ParsedVersion] = version;
            }

            _latestOrNull = CalculateLatest();
        }

        public LatestVersionInfo GetLatestVersionInfo()
        {
            if (_latestOrNull == null)
            {
                return null;
            }

            return new LatestVersionInfo(
                _latestOrNull,
                _versions[_latestOrNull].FullVersion,
                _versions
                    .Where(x => x.Value.Listed)
                    .Select(x => x.Value.FullVersion)
                    .ToArray());
        }

        public LatestIndexChanges Delete(NuGetVersion deleted)
        {
            var ctx = UpdateVersionList(
                (v, p) => _versions.Remove(v),
                deleted,
                newProperties: null);

            var searchIndexChangeType = DeleteFromSearchIndex(ctx);
            var hijackIndexChanges = DeleteFromHijackIndex(ctx);

            return new LatestIndexChanges(searchIndexChangeType, hijackIndexChanges);
        }

        /// <summary>
        /// When a non-applicable version is encountered, the search index should make sure it doesn't have that
        /// version at all (much like a <see cref="Delete(NuGetVersion)"/>). For the hijack index, the non-applicable
        /// version should not be deleted but the latest booleans should still be updated, for reflow.
        /// </summary>
        public LatestIndexChanges Remove(NuGetVersion version)
        {
            var ctx = UpdateVersionList(
                (v, p) => _versions.Remove(v),
                version,
                newProperties: null);

            var searchIndexChangeType = UpsertToSearchIndex(ctx);
            var hijackIndexChanges = UpsertToHijackIndex(ctx);

            return new LatestIndexChanges(searchIndexChangeType, hijackIndexChanges);
        }

        public LatestIndexChanges Upsert(FilteredVersionProperties addedProperties)
        {
            var ctx = UpdateVersionList(
                (v, p) => _versions[v] = p,
                addedProperties.ParsedVersion,
                addedProperties);

            var searchIndexChangeType = UpsertToSearchIndex(ctx);
            var hijackIndexChanges = UpsertToHijackIndex(ctx);

            return new LatestIndexChanges(searchIndexChangeType, hijackIndexChanges);
        }

        private static SearchIndexChangeType DeleteFromSearchIndex(Context ctx)
        {
            if (ctx.NewLatest == null)
            {
                // If there is no longer a latest version, the search document should be deleted.
                return SearchIndexChangeType.Delete;
            }

            if (ctx.NewLatest != ctx.OldLatest)
            {
                // If the latest version changes due to deletion, this can only happen if the existing latest version
                // was the one that was deleted.
                Guard.Assert(ctx.OldProperties != null, "This version should have existed before.");
                Guard.Assert(ctx.OldProperties.Listed, "The existing version should have been listed.");
                Guard.Assert(ctx.OldLatest == ctx.ChangedVersion, "The existing latest should be the deleted version.");
                return SearchIndexChangeType.DowngradeLatest;
            }

            // It's possible some cases (such as removing a version that didn't exist in the first place) could be
            // handled without any change to the search index. However, to support the reflow case, we update the
            // version list to make sure it is consistent.
            return SearchIndexChangeType.UpdateVersionList;
        }

        private static IReadOnlyList<HijackIndexChange> DeleteFromHijackIndex(Context ctx)
        {
            var changes = new List<HijackIndexChange>();

            // Delete the document for the deleted version.
            changes.Add(HijackIndexChange.Delete(ctx.ChangedVersion));

            // Update the latest status of the latest version, if there is one.
            if (ctx.NewLatest != null)
            {
                Guard.Assert(ctx.ChangedVersion != ctx.NewLatest, "The deleted version should not be the new latest version.");
                changes.Add(HijackIndexChange.SetLatestToTrue(ctx.NewLatest));
            }

            return changes;
        }

        private static SearchIndexChangeType UpsertToSearchIndex(Context ctx)
        {
            if (ctx.NewLatest == null)
            {
                // If there is no longer a latest version, the search document should be deleted.
                return SearchIndexChangeType.Delete;
            }

            if (ctx.OldLatest == null && ctx.NewLatest != null)
            {
                // If there was no latest version before but now there is a latest version, then we have just added
                // the only listed version. The new latest version is of course the version we are adding right now.
                Guard.Assert(ctx.NewLatest == ctx.ChangedVersion, "The first latest version must be the added version.");
                Guard.Assert(ctx.NewProperties.Listed, "The added version should be listed for the latest version to have changed.");
                return SearchIndexChangeType.AddFirst;
            }

            if (ctx.NewLatest == ctx.ChangedVersion)
            {
                // If the latest version has not changed or the latest version is the version we just added,
                // then we need to update the existing metadata. This includes updating the latest version string.
                return SearchIndexChangeType.UpdateLatest;
            }

            if (ctx.NewLatest < ctx.OldLatest)
            {
                // If the new latest is a lower version than the old latest, this is a special case where we need to
                // look up the old new latest's metadata.
                Guard.Assert(ctx.NewLatest != ctx.ChangedVersion, "This case should already have been handled.");
                Guard.Assert(ctx.OldLatest == ctx.ChangedVersion, "This case should already have been handled.");
                Guard.Assert(
                    ctx.NewProperties == null || !ctx.NewProperties.Listed,
                    "A downgrade from an upserted version can only happen from an unlist or removing a non-applicable version.");
                return SearchIndexChangeType.DowngradeLatest;
            }

            // It's possible some cases (such as unlisting a version that didn't exist in the first place) could be
            // handled without any change to the search index. However, to support the reflow case, we update the
            // version list to make sure it is consistent.
            return SearchIndexChangeType.UpdateVersionList;
        }

        private static IReadOnlyList<HijackIndexChange> UpsertToHijackIndex(Context ctx)
        {
            var changes = new List<HijackIndexChange>();

            // Update the metadata for the upserted version.
            changes.Add(HijackIndexChange.UpdateMetadata(ctx.ChangedVersion));

            // If the new latest is not the version that we are processing right now, explicitly set the current version
            // to not be the latest. This supports the reflow scenario.
            if (ctx.NewLatest != ctx.ChangedVersion)
            {
                changes.Add(HijackIndexChange.SetLatestToFalse(ctx.ChangedVersion));
            }

            // If the latest version has changed and the old latest version existed, mark that old latest version as
            // no longer latest.
            if (ctx.OldLatest != null
                && ctx.OldLatest != ctx.NewLatest
                && ctx.OldLatest != ctx.ChangedVersion)
            {
                changes.Add(HijackIndexChange.SetLatestToFalse(ctx.OldLatest));
            }

            // Always mark the new latest version as latest, even if it has not changed. This supports the reflow
            // scenario.
            if (ctx.NewLatest != null)
            {
                changes.Add(HijackIndexChange.SetLatestToTrue(ctx.NewLatest));
            }

            return changes;
        }

        private Context UpdateVersionList(
            Action<NuGetVersion, FilteredVersionProperties> update,
            NuGetVersion version,
            FilteredVersionProperties newProperties)
        {
            var oldLatest = _latestOrNull;

            _versions.TryGetValue(version, out var oldProperties);

            update(version, newProperties);

            _latestOrNull = CalculateLatest();

            return new Context(
                version,
                oldProperties,
                newProperties,
                oldLatest,
                _latestOrNull);
        }

        private NuGetVersion CalculateLatest()
        {
            return _versions
                .Reverse()
                .Where(x => x.Value.Listed)
                .Select(x => x.Value.ParsedVersion)
                .FirstOrDefault();
        }

        private class Context
        {
            public Context(
                NuGetVersion changedVersion,
                FilteredVersionProperties oldProperties,
                FilteredVersionProperties newProperties,
                NuGetVersion oldLatest,
                NuGetVersion newLatest)
            {
                ChangedVersion = changedVersion ?? throw new ArgumentNullException(nameof(changedVersion));
                OldProperties = oldProperties;
                NewProperties = newProperties;
                OldLatest = oldLatest;
                NewLatest = newLatest;
            }

            public NuGetVersion ChangedVersion { get; }
            public FilteredVersionProperties OldProperties { get; }
            public FilteredVersionProperties NewProperties { get; }
            public NuGetVersion OldLatest { get; }
            public NuGetVersion NewLatest { get; }
        }
    }
}

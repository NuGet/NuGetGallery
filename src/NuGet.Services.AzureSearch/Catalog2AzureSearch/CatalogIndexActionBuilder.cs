// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class CatalogIndexActionBuilder : ICatalogIndexActionBuilder
    {
        private static readonly int SearchFiltersCount = Enum.GetValues(typeof(SearchFilters)).Length;

        private readonly IVersionListDataClient _versionListDataClient;
        private readonly ICatalogLeafFetcher _leafFetcher;
        private readonly ISearchDocumentBuilder _search;
        private readonly IHijackDocumentBuilder _hijack;
        private readonly ILogger<CatalogIndexActionBuilder> _logger;

        public CatalogIndexActionBuilder(
            IVersionListDataClient versionListDataClient,
            ICatalogLeafFetcher leafFetcher,
            ISearchDocumentBuilder search,
            IHijackDocumentBuilder hijack,
            ILogger<CatalogIndexActionBuilder> logger)
        {
            _versionListDataClient = versionListDataClient ?? throw new ArgumentNullException(nameof(versionListDataClient));
            _leafFetcher = leafFetcher ?? throw new ArgumentNullException(nameof(leafFetcher));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _hijack = hijack ?? throw new ArgumentNullException(nameof(hijack));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IndexActions> AddCatalogEntriesAsync(
            string packageId,
            IReadOnlyList<CatalogCommitItem> latestEntries,
            IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> entryToLeaf)
        {
            if (latestEntries.Count == 0)
            {
                throw new ArgumentException("There must be at least one catalog item to process.", nameof(latestEntries));
            }

            var versionListDataResult = await _versionListDataClient.ReadAsync(packageId);

            var context = new Context(
                packageId,
                versionListDataResult,
                latestEntries,
                entryToLeaf);

            var indexChanges = await GetIndexChangesAsync(context);

            var search = indexChanges
                .Search
                .Select(p => GetSearchIndexAction(
                    context,
                    p.Key,
                    p.Value))
                .ToList();

            var hijack = indexChanges
                .Hijack
                .Select(p => GetHijackIndexAction(
                    context,
                    p.Key,
                    p.Value))
                .ToList();

            return new IndexActions(
                search,
                hijack,
                new ResultAndAccessCondition<VersionListData>(
                    context.VersionLists.GetVersionListData(),
                    context.VersionListDataResult.AccessCondition));
        }

        private async Task<IndexChanges> GetIndexChangesAsync(Context context)
        {
            var downgradeLatest = new List<LatestVersionInfo>();
            var versionsWithoutMetadata = new List<NuGetVersion>();
            IndexChanges indexChanges;
            var attempts = 0;
            do
            {
                attempts++;

                if (versionsWithoutMetadata.Any())
                {
                    _logger.LogInformation(
                        "The following versions are now the latest on search documents, but have no metadata in the input catalog leafs: {Versions}",
                        versionsWithoutMetadata);

                    var candidateVersionLists = downgradeLatest
                        .Select(x => x
                            .ListedFullVersions
                            .Select(NuGetVersion.Parse)
                            .ToList())
                        .GroupBy(x => x, new CollectionComparer<NuGetVersion>())
                        .Select(x => x.First())
                        .ToList();

                    var latestCatalogLeaves = await _leafFetcher.GetLatestLeavesAsync(
                        context.PackageId,
                        candidateVersionLists);

                    foreach (var pair in latestCatalogLeaves.Available)
                    {
                        var entry = GetPackageDetailsEntry(context, pair.Key, pair.Value);
                        context.VersionToEntry[pair.Key] = entry;
                        context.EntryToLeaf[entry] = pair.Value;
                    }

                    foreach (var unavailable in latestCatalogLeaves.Unavailable)
                    {
                        var entry = GetPackageDeleteEntry(context, unavailable);
                        context.VersionToEntry[unavailable] = entry;
                        context.EntryToLeaf.Remove(entry);
                    }
                }

                var versionListChanges = context
                    .VersionToEntry
                    .Values
                    .Select(e => GetVersionListChange(context, e))
                    .ToList();

                context.VersionLists = new VersionLists(context.VersionListDataResult.Result);

                indexChanges = context.VersionLists.ApplyChanges(versionListChanges);

                downgradeLatest = indexChanges
                    .Search
                    .Where(x => x.Value == SearchIndexChangeType.DowngradeLatest)
                    .Select(x => context.VersionLists.GetLatestVersionInfoOrNull(x.Key))
                    .ToList();
                versionsWithoutMetadata = downgradeLatest
                    .Where(x => !context.VersionToEntry.ContainsKey(x.ParsedVersion))
                    .Select(x => x.ParsedVersion)
                    .OrderBy(x => x)
                    .Distinct()
                    .ToList();
            }
            while (versionsWithoutMetadata.Any() && attempts < SearchFiltersCount);

            if (versionsWithoutMetadata.Any())
            {
                const string message = "Too many attempts were made to fetch metadata for downgraded search documents.";
                _logger.LogError(
                    message + " {Attempts} attempts were made for {PackageId}. Versions without metadata: {Versions}",
                    attempts,
                    context.PackageId,
                    versionsWithoutMetadata);
                throw new InvalidOperationException(message);
            }

            return indexChanges;
        }

        private VersionListChange GetVersionListChange(
            Context context,
            CatalogCommitItem entry)
        {
            if (entry.IsPackageDetails && !entry.IsPackageDelete)
            {
                var leaf = context.EntryToLeaf[entry];
                return VersionListChange.Upsert(
                    leaf.VerbatimVersion ?? leaf.PackageVersion,
                    new VersionPropertiesData(
                        listed: leaf.IsListed(),
                        semVer2: leaf.IsSemVer2()));
            }
            else if (entry.IsPackageDelete && !entry.IsPackageDetails)
            {
                return VersionListChange.Delete(entry.PackageIdentity.Version);
            }
            else
            {
                const string message = "An unsupported leaf type was encountered.";
                _logger.LogError(
                    message + " ID: {PackageId}, version: {PackageVersion}, commit timestamp: {CommitTimestamp:O}, " +
                    "types: {EntryTypeUris}, leaf URL: {Url}",
                    entry.PackageIdentity.Id,
                    entry.PackageIdentity.Version.ToFullString(),
                    entry.CommitTimeStamp,
                    entry.TypeUris,
                    entry.Uri.AbsoluteUri);
                throw new ArgumentException("An unsupported leaf type was encountered.");
            }
        }

        private IndexAction<KeyedDocument> GetSearchIndexAction(
            Context context,
            SearchFilters searchFilters,
            SearchIndexChangeType changeType)
        {
            var latestFlags = _search.LatestFlagsOrNull(context.VersionLists, searchFilters);
            Guard.Assert(
                changeType == SearchIndexChangeType.Delete || latestFlags != null,
                "Either the search document is being or there is a latest version.");

            switch (changeType)
            {
                case SearchIndexChangeType.Delete:
                    return IndexAction.Delete(_search.Keyed(
                        context.PackageId,
                        searchFilters));

                case SearchIndexChangeType.UpdateVersionList:
                    return IndexAction.Merge<KeyedDocument>(_search.UpdateVersionListFromCatalog(
                        context.PackageId,
                        searchFilters,
                        lastCommitTimestamp: context.LatestCommitTimestamp,
                        lastCommitId: context.LatestCommitId,
                        versions: latestFlags.LatestVersionInfo.ListedFullVersions,
                        isLatestStable: latestFlags.IsLatestStable,
                        isLatest: latestFlags.IsLatest));

                case SearchIndexChangeType.AddFirst:
                case SearchIndexChangeType.UpdateLatest:
                case SearchIndexChangeType.DowngradeLatest:
                    // TODO: look up owners with AddFirst.
                    // https://github.com/nuget/nugetgallery/issues/6475
                    var leaf = context.GetLeaf(latestFlags.LatestVersionInfo.ParsedVersion);
                    var normalizedVersion = VerifyConsistencyAndNormalizeVersion(context, leaf);
                    return IndexAction.MergeOrUpload<KeyedDocument>(_search.UpdateLatestFromCatalog(
                        searchFilters,
                        latestFlags.LatestVersionInfo.ListedFullVersions,
                        latestFlags.IsLatestStable,
                        latestFlags.IsLatest,
                        normalizedVersion,
                        latestFlags.LatestVersionInfo.FullVersion,
                        leaf));

                default:
                    throw new NotImplementedException($"The change type '{changeType}' is not supported.");
            }
        }

        private IndexAction<KeyedDocument> GetHijackIndexAction(
            Context context,
            NuGetVersion version,
            HijackDocumentChanges changes)
        {
            if (changes.Delete)
            {
                return IndexAction.Delete(_hijack.Keyed(
                    context.PackageId,
                    version.ToNormalizedString()));
            }

            if (!changes.UpdateMetadata)
            {
                return IndexAction.Merge<KeyedDocument>(_hijack.LatestFromCatalog(
                    context.PackageId,
                    version.ToNormalizedString(),
                    lastCommitTimestamp: context.LatestCommitTimestamp,
                    lastCommitId: context.LatestCommitId,
                    changes: changes));
            }

            var leaf = context.GetLeaf(version);
            var normalizedVersion = VerifyConsistencyAndNormalizeVersion(context, leaf);

            return IndexAction.MergeOrUpload<KeyedDocument>(_hijack.FullFromCatalog(
                normalizedVersion,
                changes,
                leaf));
        }

        private string VerifyConsistencyAndNormalizeVersion(
            Context context,
            PackageDetailsCatalogLeaf leaf)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(context.PackageId, leaf.PackageId))
            {
                const string message = "The package ID found in the catalog package does not match the catalog leaf.";
                _logger.LogError(
                    message + " Page ID: {PagePackageId}, leaf ID: {LeafPackageId}, leaf URL: {Url}",
                    context.PackageId,
                    leaf.PackageId,
                    leaf.Url);
                throw new InvalidOperationException(message);
            }

            var parsedPackageVersion = leaf.ParsePackageVersion();
            var normalizedVersion = parsedPackageVersion.ToNormalizedString();
            if (leaf.VerbatimVersion != null)
            {
                var parsedVerbatimVersion = NuGetVersion.Parse(leaf.VerbatimVersion);
                if (normalizedVersion != parsedVerbatimVersion.ToNormalizedString())
                {
                    const string message =
                        "The normalized versions from the package version and the verbatim version do not match.";
                    _logger.LogError(
                        message + " ID: {PackageId}, version: {PackageVersion}, verbatim: {VerbatimVersion}, leaf URL: {Url}",
                        leaf.PackageId,
                        leaf.PackageVersion,
                        leaf.VerbatimVersion,
                        leaf.Url);
                    throw new InvalidOperationException(message);
                }
            }

            if (parsedPackageVersion.IsPrerelease != leaf.IsPrerelease)
            {
                var message =
                    $"The {nameof(PackageDetailsCatalogLeaf.IsPrerelease)} from the leaf does not match the version. " +
                    $"Using the value from the parsed version. ";
                _logger.LogWarning(
                    message + " ID: {PackageId}, version: {PackageVersion}, leaf is prerelease: {LeafIsPrerelease}, " +
                    "parsed is prerelease: {ParsedIsPrerelease}, leaf URL: {Url}",
                    leaf.PackageId,
                    leaf.PackageVersion,
                    leaf.IsPrerelease,
                    parsedPackageVersion.IsPrerelease,
                    leaf.Url);
                leaf.IsPrerelease = parsedPackageVersion.IsPrerelease;
            }

            return normalizedVersion;
        }

        private CatalogCommitItem GetPackageDetailsEntry(Context context, NuGetVersion version, PackageDetailsCatalogLeaf leaf)
        {
            return new CatalogCommitItem(
                uri: new Uri(leaf.Url, UriKind.Absolute),
                commitId: leaf.CommitId,
                commitTimeStamp: leaf.CommitTimestamp.UtcDateTime,
                types: new string[0],
                typeUris: new[] { Schema.DataTypes.PackageDetails },
                packageIdentity: new PackageIdentity(leaf.PackageId, version));
        }

        private CatalogCommitItem GetPackageDeleteEntry(Context context, NuGetVersion version)
        {
            return new CatalogCommitItem(
                uri: null,
                commitId: null,
                commitTimeStamp: DateTime.MinValue,
                types: new string[0],
                typeUris: new[] { Schema.DataTypes.PackageDelete },
                packageIdentity: new PackageIdentity(context.PackageId, version));
        }

        private class Context
        {
            public Context(
                string packageId,
                ResultAndAccessCondition<VersionListData> versionListDataResult,
                IEnumerable<CatalogCommitItem> latestEntries,
                IReadOnlyDictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> entryToLeaf)
            {
                PackageId = packageId;
                VersionListDataResult = versionListDataResult;
                VersionToEntry = latestEntries.ToDictionary(x => x.PackageIdentity.Version);
                EntryToLeaf = entryToLeaf.ToDictionary(
                    x => x.Key,
                    x => x.Value,
                    ReferenceEqualityComparer<CatalogCommitItem>.Default);

                var latestCommit = latestEntries
                    .GroupBy(x => new { x.CommitTimeStamp, x.CommitId })
                    .Select(x => x.Key)
                    .OrderByDescending(x => x.CommitTimeStamp)
                    .First();
                LatestCommitTimestamp = new DateTimeOffset(latestCommit.CommitTimeStamp.ToUniversalTime());
                LatestCommitId = latestCommit.CommitId;
            }

            public string PackageId { get; }
            public ResultAndAccessCondition<VersionListData> VersionListDataResult { get; }
            public Dictionary<NuGetVersion, CatalogCommitItem> VersionToEntry { get; }
            public Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf> EntryToLeaf { get; }
            public VersionLists VersionLists { get; set; }
            public DateTimeOffset LatestCommitTimestamp { get; }
            public string LatestCommitId { get; }

            public PackageDetailsCatalogLeaf GetLeaf(NuGetVersion version)
            {
                var entry = VersionToEntry[version];
                if (entry.IsPackageDelete)
                {
                    throw new ArgumentException("Leaves are not fetched for deleted versions.", nameof(version));
                }

                return EntryToLeaf[entry];
            }
        }

        private class CollectionComparer<T> : IEqualityComparer<IReadOnlyCollection<T>>
        {
            public bool Equals(IReadOnlyCollection<T> x, IReadOnlyCollection<T> y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(IReadOnlyCollection<T> obj)
            {
                return obj
                    .OrderBy(x => x)
                    .Aggregate(0, (sum, i) => unchecked(sum + (i?.GetHashCode() ?? 0)));
            }
        }
    }
}

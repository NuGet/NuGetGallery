// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class DocumentFixUpEvaluator : IDocumentFixUpEvaluator
    {
        private readonly IVersionListDataClient _versionListClient;
        private readonly ICatalogLeafFetcher _leafFetcher;
        private readonly ILogger<DocumentFixUpEvaluator> _logger;

        public DocumentFixUpEvaluator(
            IVersionListDataClient versionListClient,
            ICatalogLeafFetcher leafFetcher,
            ILogger<DocumentFixUpEvaluator> logger)
        {
            _versionListClient = versionListClient ?? throw new ArgumentNullException(nameof(versionListClient));
            _leafFetcher = leafFetcher ?? throw new ArgumentNullException(nameof(leafFetcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DocumentFixUp> TryFixUpAsync(
            IReadOnlyList<CatalogCommitItem> itemList,
            ConcurrentBag<IdAndValue<IndexActions>> allIndexActions,
            InvalidOperationException exception)
        {
            var innerEx = exception.InnerException as IndexBatchException;
            if (innerEx == null || innerEx.IndexingResults == null)
            {
                return DocumentFixUp.IsNotApplicable();
            }

            // There may have been a Case of the Missing Document! We have confirmed with the Azure Search team that
            // this is a bug on the Azure Search side. To mitigate the issue, we replace any Merge operation that
            // failed with 404 with a MergeOrUpload with the full metadata so that we can replace that missing document.
            //
            // 1. The first step is to find all of the document keys that failed with a 404 Not Found error.
            var notFoundKeys = new HashSet<string>(innerEx
                .IndexingResults
                .Where(x => x.StatusCode == (int)HttpStatusCode.NotFound)
                .Select(x => x.Key));
            if (!notFoundKeys.Any())
            {
                return DocumentFixUp.IsNotApplicable();
            }

            _logger.LogWarning("{Count} document action(s) failed with 404 Not Found.", notFoundKeys.Count);

            // 2. Find all of the package IDs that were affected, only considering Merge operations against the Search
            //    index. We ignore the the hijack index for now because we have only ever seen the problem in the Search
            //    index.
            var failedIds = new HashSet<string>();
            foreach (var pair in allIndexActions.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
            {
                var failedMerges = pair
                    .Value
                    .Search
                    .Where(a => a.ActionType == IndexActionType.Merge)
                    .Where(a => notFoundKeys.Contains(a.Document.Key));

                if (failedMerges.Any() && failedIds.Add(pair.Id))
                {
                    _logger.LogWarning("Package {PackageId} had a Merge operation fail with 404 Not Found.", pair.Id);
                }
            }

            if (!failedIds.Any())
            {
                _logger.LogInformation("No failed Merge operations against the Search index were found.");
                return DocumentFixUp.IsNotApplicable();
            }

            // 3. For each affected package ID, get the version list to determine the latest version per search filter
            //    so we can find the the catalog entry for the version.
            var identityToItems = itemList.GroupBy(x => x.PackageIdentity).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var packageId in failedIds)
            {
                var accessConditionAndData = await _versionListClient.ReadAsync(packageId);
                var versionLists = new VersionLists(accessConditionAndData.Result);

                var latestVersions = DocumentUtilities
                    .AllSearchFilters
                    .Select(sf => versionLists.GetLatestVersionInfoOrNull(sf))
                    .Where(lvi => lvi != null)
                    .Select(lvi => (IReadOnlyList<NuGetVersion>)new List<NuGetVersion> { lvi.ParsedVersion })
                    .ToList();

                var leaves = await _leafFetcher.GetLatestLeavesAsync(packageId, latestVersions);

                // We ignore unavailable (deleted) versions for now. We have never had a delete cause this problem. It's
                // only ever been discovered when a new version is being added or updated.
                //
                // For each package details leaf found, create a catalog commit item and add it to the set of items we
                // will process. This will force the metadata to be updated on each of the latest versions. Since this
                // is the latest metadata, replace any older leaves that may be associated with that package version.
                foreach (var pair in leaves.Available)
                {
                    var identity = new PackageIdentity(packageId, pair.Key);
                    var leaf = pair.Value;

                    if (identityToItems.TryGetValue(identity, out var existing))
                    {
                        if (existing.Count == 1 && existing[0].Uri.AbsoluteUri == leaf.Url)
                        {
                            _logger.LogInformation(
                                "For {PackageId} {PackageVersion}, metadata will remain the same.",
                                identity.Id,
                                identity.Version.ToNormalizedString());
                            continue;
                        }
                        else
                        {
                            _logger.LogInformation(
                                "For {PackageId} {PackageVersion}, metadata from {Url} will be used instead of {Count} catalog commit items.",
                                identity.Id,
                                identity.Version.ToNormalizedString(),
                                leaf.Url,
                                existing.Count);
                        }
                    }
                    else
                    {
                        _logger.LogInformation(
                            "For {PackageId} {PackageVersion}, metadata from {Url} will be used.",
                            identity.Id,
                            identity.Version.ToNormalizedString(),
                            leaf.Url);
                    }

                    identityToItems[identity] = new List<CatalogCommitItem>
                    {
                        new CatalogCommitItem(
                            new Uri(leaf.Url),
                            leaf.CommitId,
                            leaf.CommitTimestamp.UtcDateTime,
                            new string[0],
                            new[] { Schema.DataTypes.PackageDetails },
                            identity),
                    };
                }
            }

            _logger.LogInformation("The catalog commit item list has been modified to fix up the missing document(s).");

            var newItemList = identityToItems.SelectMany(x => x.Value).ToList();
            return DocumentFixUp.IsApplicable(newItemList);
        }
    }
}

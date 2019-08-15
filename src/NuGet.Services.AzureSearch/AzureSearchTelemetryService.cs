// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.Logging;

namespace NuGet.Services.AzureSearch
{
    public class AzureSearchTelemetryService : IAzureSearchTelemetryService
    {
        private const string Prefix = "AzureSearch.";

        private readonly ITelemetryClient _telemetryClient;

        public AzureSearchTelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public IDisposable TrackVersionListsUpdated(int versionListCount, int workerCount)
        {
            return _telemetryClient.TrackDuration(
                Prefix + "VersionListsUpdatedSeconds",
                new Dictionary<string, string>
                {
                    { "VersionListCount", versionListCount.ToString() },
                    { "WorkerCount", workerCount.ToString() },
                });
        }

        public void TrackIndexPushSuccess(string indexName, int documentCount, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "IndexPushSuccessSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "IndexName", indexName },
                    { "DocumentCount", documentCount.ToString() },
                });
        }

        public void TrackIndexPushFailure(string indexName, int documentCount, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "IndexPushFailureSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "IndexName", indexName },
                    { "DocumentCount", documentCount.ToString() },
                });
        }

        public void TrackIndexPushSplit(string indexName, int documentCount)
        {
            _telemetryClient.TrackMetric(
                Prefix + "IndexPushSplit",
                1,
                new Dictionary<string, string>
                {
                    { "IndexName", indexName },
                    { "DocumentCount", documentCount.ToString() },
                });
        }

        public IDisposable TrackGetLatestLeaves(string packageId, int requestedVersions)
        {
            return _telemetryClient.TrackDuration(
                Prefix + "GetLatestLeavesSeconds",
                new Dictionary<string, string>
                {
                    { "PackageId", packageId },
                    { "RequestVersions", requestedVersions.ToString() },
                });
        }

        public void TrackOwners2AzureSearchCompleted(JobOutcome outcome, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "Owners2AzureSearchCompletedSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "Outcome", outcome.ToString() },
                });
        }

        public void TrackAuxiliaryFilesReload(IReadOnlyList<string> reloadedNames, IReadOnlyList<string> notModifiedNames, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "AuxiliaryFilesReloadSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "ReloadedNames", JsonConvert.SerializeObject(reloadedNames) },
                    { "NotModifiedNames", JsonConvert.SerializeObject(notModifiedNames) },
                });
        }

        public void TrackAuxiliaryFileDownloaded(string blobName, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "AuxiliaryFileDownloadedSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "BlobName", blobName },
                });
        }

        public void TrackGetOwnersForPackageId(int ownerCount, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "GetOwnersForPackageIdSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "OwnerCount", ownerCount.ToString() },
                });
        }

        public void TrackReadLatestIndexedOwners(int packageIdCount, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "ReadLatestIndexedOwnersSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "PackageIdCount", packageIdCount.ToString() },
                });
        }

        public IDisposable TrackUploadOwnerChangeHistory(int packageIdCount)
        {
            return _telemetryClient.TrackDuration(
                Prefix + "UploadOwnerChangeHistorySeconds",
                new Dictionary<string, string>
                {
                    { "PackageIdCount", packageIdCount.ToString() },
                });
        }

        public IDisposable TrackReplaceLatestIndexedOwners(int packageIdCount)
        {
            return _telemetryClient.TrackDuration(
                Prefix + "ReplaceLatestIndexedOwnersSeconds",
                new Dictionary<string, string>
                {
                    { "PackageIdCount", packageIdCount.ToString() },
                });
        }

        public void TrackReadLatestOwnersFromDatabase(int packageIdCount, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "ReadLatestOwnersFromDatabaseSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "PackageIdCount", packageIdCount.ToString() },
                });
        }

        public void TrackOwnerSetComparison(int oldCount, int newCount, int changeCount, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "OwnerSetComparisonSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "OldCount", oldCount.ToString() },
                    { "NewCount", newCount.ToString() },
                    { "ChangeCount", changeCount.ToString() },
                });
        }

        public IDisposable TrackCatalog2AzureSearchProcessBatch(int catalogLeafCount, int latestCatalogLeafCount, int packageIdCount)
        {
            return _telemetryClient.TrackDuration(
                Prefix + "Catalog2AzureSearchBatchSeconds",
                new Dictionary<string, string>
                {
                    { "CatalogLeafCount", catalogLeafCount.ToString() },
                    { "LatestCatalogLeafCount", latestCatalogLeafCount.ToString() },
                    { "PackageIdCount", packageIdCount.ToString() },
                });
        }

        public void TrackV2SearchQueryWithSearchIndex(TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "V2SearchQueryWithSearchIndexMs",
                elapsed.TotalMilliseconds);
        }

        public void TrackV2SearchQueryWithHijackIndex(TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "V2SearchQueryWithHijackIndexMs",
                elapsed.TotalMilliseconds);
        }

        public void TrackAutocompleteQuery(TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "AutocompleteQueryMs",
                elapsed.TotalMilliseconds);
        }

        public void TrackV3SearchQuery(TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "V3SearchQueryMs",
                elapsed.TotalMilliseconds);
        }

        public void TrackGetSearchServiceStatus(SearchStatusOptions options, bool success, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "GetSearchServiceStatusMs",
                elapsed.TotalMilliseconds,
                new Dictionary<string, string>
                {
                    { "Options", options.ToString() },
                    { "Success", success.ToString() },
                });
        }

        public IDisposable TrackCatalogLeafDownloadBatch(int count)
        {
            return _telemetryClient.TrackDuration(
                Prefix + "CatalogLeafDownloadBatchSeconds",
                new Dictionary<string, string>
                {
                    { "Count", count.ToString() },
                });
        }

        public void TrackDocumentCountQuery(string indexName, long count, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "DocumentCountQueryMs",
                elapsed.TotalMilliseconds,
                new Dictionary<string, string>
                {
                    { "IndexName", indexName },
                    { "Count", count.ToString() },
                });
        }

        public void TrackWarmQuery(string indexName, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "WarmQueryMs",
                elapsed.TotalMilliseconds,
                new Dictionary<string, string>
                {
                    { "IndexName", indexName },
                });
        }

        public void TrackLastCommitTimestampQuery(string indexName, DateTimeOffset? lastCommitTimestamp, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "LastCommitTimestampQueryMs",
                elapsed.TotalMilliseconds,
                new Dictionary<string, string>
                {
                    { "IndexName", indexName },
                    { "LastCommitTimestamp", lastCommitTimestamp?.ToString("O") },
                });
        }

        public void TrackReadLatestIndexedDownloads(int? packageIdCount, bool notModified, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "ReadLatestIndexedDownloadsSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "PackageIdCount", packageIdCount?.ToString() },
                    { "NotModified", notModified.ToString() },
                });
        }

        public IDisposable TrackReplaceLatestIndexedDownloads(int packageIdCount)
        {
            return _telemetryClient.TrackDuration(
                Prefix + "ReplaceLatestIndexedDownloadsSeconds",
                new Dictionary<string, string>
                {
                    { "PackageIdCount", packageIdCount.ToString() },
                });
        }

        public void TrackDownloadSetComparison(int oldCount, int newCount, int changeCount, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "DownloadSetComparisonSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "OldCount", oldCount.ToString() },
                    { "NewCount", newCount.ToString() },
                    { "ChangeCount", changeCount.ToString() },
                });
        }

        public void TrackDownloadCountDecrease(
            string packageId,
            string version,
            bool oldHasId,
            bool oldHasVersion,
            long oldDownloads,
            bool newHasId,
            bool newHasVersion,
            long newDownloads)
        {
            _telemetryClient.TrackMetric(
                Prefix + "DownloadCountDecrease",
                1,
                new Dictionary<string, string>
                {
                    { "PackageId", packageId },
                    { "Version", version },
                    { "OldHasId", oldHasId.ToString() },
                    { "OldHasVersion", oldHasVersion.ToString() },
                    { "OldDownloads", oldDownloads.ToString() },
                    { "NewHasId", newHasId.ToString() },
                    { "NewHasVersion", newHasVersion.ToString() },
                    { "NewDownloads", newDownloads.ToString() },
                });
        }

        public void TrackAuxiliary2AzureSearchCompleted(JobOutcome outcome, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "Auxiliary2AzureSearchCompletedSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "Outcome", outcome.ToString() },
                });
        }

        public void TrackV3GetDocument(TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "V3GetDocumentMs",
                elapsed.TotalMilliseconds);
        }

        public void TrackV2GetDocumentWithSearchIndex(TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "V2GetDocumentWithSearchIndexMs",
                elapsed.TotalMilliseconds);
        }

        public void TrackV2GetDocumentWithHijackIndex(TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "V2GetDocumentWithHijackIndexMs",
                elapsed.TotalMilliseconds);
        }

        public void TrackReadLatestVerifiedPackages(int? packageIdCount, bool notModified, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "ReadLatestVerifiedPackagesSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "PackageIdCount", packageIdCount?.ToString() },
                    { "NotModified", notModified.ToString() },
                });
        }

        public IDisposable TrackReplaceLatestVerifiedPackages(int packageIdCount)
        {
            return _telemetryClient.TrackDuration(
                Prefix + "ReplaceLatestVerifiedPackagesSeconds",
                new Dictionary<string, string>
                {
                    { "PackageIdCount", packageIdCount.ToString() },
                });
        }

        public void TrackAuxiliaryFilesStringCache(int stringCount, long charCount, int requestCount, int hitCount)
        {
            _telemetryClient.TrackMetric(
                Prefix + "AuxiliaryFilesStringCache",
                1,
                new Dictionary<string, string>
                {
                    { "StringCount", stringCount.ToString() },
                    { "CharCount", charCount.ToString() },
                    { "RequestCount", requestCount.ToString() },
                    { "HitCount", hitCount.ToString() },
                });
        }
    }
}

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

        public void TrackOwners2AzureSearchCompleted(bool success, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "Owners2AzureSearchCompletedSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "Success", success.ToString() },
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

        public void TrackReadLatestIndexedOwners(int ownerCount, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "ReadLatestIndexedOwnersSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "OwnerCount", ownerCount.ToString() },
                });
        }

        public void TrackAuxiliaryFileNotModified(string blobName, TimeSpan elapsed)
        {
            _telemetryClient.TrackMetric(
                Prefix + "AuxiliaryFileNotModifiedSeconds",
                elapsed.TotalSeconds,
                new Dictionary<string, string>
                {
                    { "BlobName", blobName },
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
    }
}

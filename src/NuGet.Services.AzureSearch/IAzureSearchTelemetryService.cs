// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.AzureSearch.SearchService;

namespace NuGet.Services.AzureSearch
{
    public interface IAzureSearchTelemetryService
    {
        void TrackAuxiliaryFileDownloaded(string blobName, TimeSpan elapsed);
        void TrackAuxiliaryFilesReload(IReadOnlyList<string> reloadedNames, IReadOnlyList<string> notModifiedNames, TimeSpan elapsed);
        IDisposable TrackGetLatestLeaves(string packageId, int requestedVersions);
        void TrackGetOwnersForPackageId(int ownerCount, TimeSpan elapsed);
        void TrackIndexPushFailure(string indexName, int documentCount, TimeSpan elapsed);
        void TrackIndexPushSplit(string indexName, int documentCount);
        void TrackIndexPushSuccess(string indexName, int documentCount, TimeSpan elapsed);
        void TrackUpdateOwnersCompleted(JobOutcome outcome, TimeSpan elapsed);
        void TrackOwnerSetComparison(int oldCount, int newCount, int changeCount, TimeSpan elapsed);
        void TrackReadLatestIndexedOwners(int packageIdCount, TimeSpan elapsed);
        void TrackReadLatestOwnersFromDatabase(int packageIdCount, TimeSpan elapsed);
        void TrackReadLatestIndexedPopularityTransfers(int outgoingTransfers, TimeSpan elapsed);
        void TrackReadLatestVerifiedPackagesFromDatabase(int packageIdCount, TimeSpan elapsed);
        IDisposable TrackReplaceLatestIndexedOwners(int packageIdCount);
        IDisposable TrackUploadOwnerChangeHistory(int packageIdCount);
        IDisposable TrackReplaceLatestIndexedPopularityTransfers(int outgoingTransfers);
        IDisposable TrackVersionListsUpdated(int versionListCount, int workerCount);
        IDisposable TrackCatalog2AzureSearchProcessBatch(int catalogLeafCount, int latestCatalogLeafCount, int packageIdCount);
        void TrackV2SearchQueryWithSearchIndex(TimeSpan elapsed);
        void TrackV2SearchQueryWithHijackIndex(TimeSpan elapsed);
        void TrackAutocompleteQuery(TimeSpan elapsed);
        void TrackDownloadSetComparison(int oldCount, int newCount, int changeCount, TimeSpan elapsed);
        void TrackV3SearchQuery(TimeSpan elapsed);
        void TrackGetSearchServiceStatus(SearchStatusOptions options, bool success, TimeSpan elapsed);
        void TrackDocumentCountQuery(string indexName, long count, TimeSpan elapsed);
        void TrackDownloadCountDecrease(
            string packageId,
            string version,
            bool oldHasId,
            bool oldHasVersion,
            long oldDownloads,
            bool newHasId,
            bool newHasVersion,
            long newDownloads);
        void TrackWarmQuery(string indexName, TimeSpan elapsed);
        void TrackLastCommitTimestampQuery(string indexName, DateTimeOffset? lastCommitTimestamp, TimeSpan elapsed);
        void TrackReadLatestIndexedDownloads(int? packageIdCount, bool notModified, TimeSpan elapsed);
        IDisposable TrackReplaceLatestIndexedDownloads(int packageIdCount);
        void TrackAuxiliary2AzureSearchCompleted(JobOutcome outcome, TimeSpan elapsed);
        void TrackV3GetDocument(TimeSpan elapsed);
        void TrackV2GetDocumentWithSearchIndex(TimeSpan elapsed);
        void TrackV2GetDocumentWithHijackIndex(TimeSpan elapsed);
        void TrackUpdateVerifiedPackagesCompleted(JobOutcome outcome, TimeSpan elapsed);
        void TrackReadLatestVerifiedPackages(int? packageIdCount, bool notModified, TimeSpan elapsed);
        IDisposable TrackReplaceLatestVerifiedPackages(int packageIdCount);
        void TrackAuxiliaryFilesStringCache(int stringCount, long charCount, int requestCount, int hitCount);
        void TrackUpdateDownloadsCompleted(JobOutcome outcome, TimeSpan elapsed);
    }
}
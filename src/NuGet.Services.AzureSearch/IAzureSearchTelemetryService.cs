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
        void TrackAuxiliaryFileNotModified(string blobName, TimeSpan elapsed);
        void TrackAuxiliaryFilesReload(IReadOnlyList<string> reloadedNames, IReadOnlyList<string> notModifiedNames, TimeSpan elapsed);
        IDisposable TrackGetLatestLeaves(string packageId, int requestedVersions);
        void TrackGetOwnersForPackageId(int ownerCount, TimeSpan elapsed);
        void TrackIndexPushFailure(string indexName, int documentCount, TimeSpan elapsed);
        void TrackIndexPushSplit(string indexName, int documentCount);
        void TrackIndexPushSuccess(string indexName, int documentCount, TimeSpan elapsed);
        void TrackOwners2AzureSearchCompleted(bool success, TimeSpan elapsed);
        void TrackOwnerSetComparison(int oldCount, int newCount, int changeCount, TimeSpan elapsed);
        void TrackReadLatestIndexedOwners(int ownerCount, TimeSpan elapsed);
        void TrackReadLatestOwnersFromDatabase(int packageIdCount, TimeSpan elapsed);
        IDisposable TrackReplaceLatestIndexedOwners(int packageIdCount);
        IDisposable TrackUploadOwnerChangeHistory(int packageIdCount);
        IDisposable TrackVersionListsUpdated(int versionListCount, int workerCount);
        IDisposable TrackCatalog2AzureSearchProcessBatch(int catalogLeafCount, int latestCatalogLeafCount, int packageIdCount);
        void TrackV2SearchQueryWithSearchIndex(TimeSpan duration);
        void TrackV2SearchQueryWithHijackIndex(TimeSpan duration);
        void TrackAutocompleteQuery(TimeSpan duration);
        void TrackV3SearchQuery(TimeSpan duration);
        void TrackGetSearchServiceStatus(SearchStatusOptions options, bool success, TimeSpan duration);
        IDisposable TrackCatalogLeafDownloadBatch(int count);
    }
}
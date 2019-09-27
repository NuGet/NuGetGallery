// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog
{
    public interface ITelemetryService
    {
        /// <summary>
        /// Allows setting dimensions that will be added to all telemetry emited by the job.
        /// </summary>
        IDictionary<string, string> GlobalDimensions { get; }

        void TrackCatalogIndexWriteDuration(TimeSpan duration, Uri uri);
        void TrackCatalogIndexReadDuration(TimeSpan duration, Uri uri);

        IDisposable TrackIndexCommitDuration();
        void TrackIndexCommitTimeout();

        void TrackHandlerFailedToProcessPackage(IPackagesContainerHandler handler, string packageId, NuGetVersion packageVersion);
        void TrackPackageMissingHash(string packageId, NuGetVersion packageVersion);
        void TrackPackageHasIncorrectHash(string packageId, NuGetVersion packageVersion);
        void TrackPackageAlreadyHasHash(string packageId, NuGetVersion packageVersion);
        void TrackPackageHashFixed(string packageId, NuGetVersion packageVersion);

        void TrackMetric(string name, ulong metric, IDictionary<string, string> properties = null);
        DurationMetric TrackDuration(string name, IDictionary<string, string> properties = null);
        IDisposable TrackExternalIconProcessingDuration(string packageId, string normalizedPackageVersion);
        IDisposable TrackEmbeddedIconProcessingDuration(string packageId, string normalizedPackageVersion);
        void TrackIconDeletionSuccess(string packageId, string normalizedPackageVersion);
        void TrackIconDeletionFailure(string packageId, string normalizedPackageVersion);
        void TrackExternalIconIngestionSuccess(string packageId, string normalizedPackageVersion);
        void TrackExternalIconIngestionFailure(string packageId, string normalizedPackageVersion);
        void TrackIconExtractionSuccess(string packageId, string normalizedPackageVersion);
        void TrackIconExtractionFailure(string packageId, string normalizedPackageVersion);
        IDisposable TrackGetPackageDetailsQueryDuration(Db2CatalogCursor cursor);
        IDisposable TrackGetPackageQueryDuration(string packageId, string packageVersion);
    }
}
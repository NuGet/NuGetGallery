// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using NuGet.Services.Logging;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog
{
    public class TelemetryService : ITelemetryService
    {
        private readonly TelemetryClientWrapper _telemetryClient;

        public IDictionary<string, string> GlobalDimensions { get; }

        public TelemetryService(TelemetryClient telemetryClient)
        {
            if (telemetryClient == null)
            {
                throw new ArgumentNullException(nameof(telemetryClient));
            }

            _telemetryClient = new TelemetryClientWrapper(telemetryClient);
            GlobalDimensions = new Dictionary<string, string>();
        }

        public void TrackCatalogIndexReadDuration(TimeSpan duration, Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                TelemetryConstants.CatalogIndexReadDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Uri, uri.AbsoluteUri },
                });
        }

        public void TrackCatalogIndexWriteDuration(TimeSpan duration, Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                TelemetryConstants.CatalogIndexWriteDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Uri, uri.AbsoluteUri },
                });
        }

        public IDisposable TrackIndexCommitDuration()
        {
            return _telemetryClient.TrackDuration(TelemetryConstants.IndexCommitDurationSeconds);
        }

        public void TrackIndexCommitTimeout()
        {
            _telemetryClient.TrackMetric(TelemetryConstants.IndexCommitTimeout, 1);
        }

        public void TrackHandlerFailedToProcessPackage(IPackagesContainerHandler handler, string packageId, NuGetVersion packageVersion)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException($"The package id parameter is required", nameof(packageId));
            }

            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            _telemetryClient.TrackMetric(
                TelemetryConstants.HandlerFailedToProcessPackage,
                1,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Handler, handler.GetType().Name },
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.Version, packageVersion.ToNormalizedString() }
                });
        }

        public void TrackPackageMissingHash(string packageId, NuGetVersion packageVersion)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException($"The package id parameter is required", nameof(packageId));
            }

            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            _telemetryClient.TrackMetric(
                TelemetryConstants.PackageMissingHash,
                1,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.Version, packageVersion.ToNormalizedString() }
                });
        }

        public void TrackPackageHasIncorrectHash(string packageId, NuGetVersion packageVersion)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException($"The package id parameter is required", nameof(packageId));
            }

            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            _telemetryClient.TrackMetric(
                TelemetryConstants.PackageHasIncorrectHash,
                1,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.Version, packageVersion.ToNormalizedString() }
                });
        }

        public void TrackPackageAlreadyHasHash(string packageId, NuGetVersion packageVersion)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException($"The package id parameter is required", nameof(packageId));
            }

            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            _telemetryClient.TrackMetric(
                TelemetryConstants.PackageAlreadyHasHash,
                1,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.Version, packageVersion.ToNormalizedString() }
                });
        }

        public void TrackPackageHashFixed(string packageId, NuGetVersion packageVersion)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException($"The package id parameter is required", nameof(packageId));
            }

            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            _telemetryClient.TrackMetric(
                TelemetryConstants.PackageHashFixed,
                1,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.Version, packageVersion.ToNormalizedString() }
                });
        }

        public void TrackMetric(string name, ulong metric, IDictionary<string, string> properties = null)
        {
            _telemetryClient.TrackMetric(name, metric, properties);
        }

        public virtual DurationMetric TrackDuration(string name, IDictionary<string, string> properties = null)
        {
            return new DurationMetric(_telemetryClient, name, properties);
        }

        public void TrackIconExtractionFailure(string packageId, string normalizedPackageVersion)
        {
            _telemetryClient.TrackMetric(
                TelemetryConstants.IconExtractionFailed,
                1,
                new Dictionary<string, string>
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.Version, normalizedPackageVersion }
                });
        }
    }
}
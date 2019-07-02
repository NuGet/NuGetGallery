// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Logging;

namespace NuGetGallery
{
    public class DownloadCountObjectMaterializedInterceptor
        : IObjectMaterializedInterceptor
    {
        private const string TelemetrySourcePrefix = "DownloadCountObjectMaterializedInterceptor.";

        private const string PackageIdDimensionName = "PackageId";
        private const string PackageVersionDimensionName = "PackageVersion";
        private const string TelemetryOriginDimensionName = "Origin";

        private const string GalleryDownloadCountLargerThanDownloadJsonByMetricName = TelemetrySourcePrefix + "GalleryDownloadCountLargerThanDownloadJsonBy";

        private readonly ITelemetryClient _telemetryClient;
        private readonly IDownloadCountService _downloadCountService;

        public DownloadCountObjectMaterializedInterceptor(IDownloadCountService downloadCountService, ITelemetryClient telemetryClient)
        {
            _downloadCountService = downloadCountService;
            _telemetryClient = telemetryClient;
        }

        public void InterceptObjectMaterialized(object entity)
        {
            InterceptPackageMaterialized(entity as Package);
            InterceptPackageRegistrationMaterialized(entity as PackageRegistration);
        }

        protected void InterceptPackageMaterialized(Package package)
        {
            if (package == null || package.PackageRegistration == null)
            {
                return;
            }

            var packageNormalizedVersion = String.IsNullOrEmpty(package.NormalizedVersion)
                ? NuGetVersionFormatter.Normalize(package.Version)
                : package.NormalizedVersion;

            int downloadCount;
            if (_downloadCountService.TryGetDownloadCountForPackage(package.PackageRegistration.Id, packageNormalizedVersion, out downloadCount))
            {
                if (downloadCount < package.DownloadCount)
                {
                    _telemetryClient.TrackMetric(GalleryDownloadCountLargerThanDownloadJsonByMetricName, package.DownloadCount - downloadCount, new Dictionary<string, string>
                        {
                            { TelemetryOriginDimensionName, TelemetrySourcePrefix + "InterceptPackageMaterialized" },
                            { PackageIdDimensionName, package.PackageRegistration.Id },
                            { PackageVersionDimensionName, packageNormalizedVersion }
                        });
                }

                package.DownloadCount = downloadCount;
            }
        }

        protected void InterceptPackageRegistrationMaterialized(PackageRegistration packageRegistration)
        {
            if (packageRegistration == null)
            {
                return;
            }

            int downloadCount;
            if (_downloadCountService.TryGetDownloadCountForPackageRegistration(packageRegistration.Id, out downloadCount))
            {
                if (downloadCount < packageRegistration.DownloadCount)
                {
                    _telemetryClient.TrackMetric(GalleryDownloadCountLargerThanDownloadJsonByMetricName, packageRegistration.DownloadCount - downloadCount, new Dictionary<string, string>
                        {
                            { TelemetryOriginDimensionName,  TelemetrySourcePrefix + "InterceptPackageRegistrationMaterialized" },
                            { PackageIdDimensionName, packageRegistration.Id },
                            { PackageVersionDimensionName, "" }
                        });
                }

                packageRegistration.DownloadCount = downloadCount;
            }
        }
    }
}
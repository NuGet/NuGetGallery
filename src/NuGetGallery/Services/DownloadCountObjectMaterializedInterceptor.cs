// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class DownloadCountObjectMaterializedInterceptor
        : IObjectMaterializedInterceptor
    {
        private readonly ITelemetryService _telemetryService;
        private readonly IDownloadCountService _downloadCountService;

        public DownloadCountObjectMaterializedInterceptor(IDownloadCountService downloadCountService, ITelemetryService telemetryService)
        {
            _downloadCountService = downloadCountService ?? throw new ArgumentNullException(nameof(downloadCountService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
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

            if (_downloadCountService.TryGetDownloadCountForPackage(package.PackageRegistration.Id, packageNormalizedVersion, out var downloadCount))
            {
                if (downloadCount < package.DownloadCount)
                {
                    _telemetryService.TrackPackageDownloadCountDecreasedFromGallery(package.PackageRegistration.Id, packageNormalizedVersion, package.DownloadCount, downloadCount);
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

            if (_downloadCountService.TryGetDownloadCountForPackageRegistration(packageRegistration.Id, out var downloadCount))
            {
                if (downloadCount < packageRegistration.DownloadCount)
                {
                    _telemetryService.TrackPackageRegistrationDownloadCountDecreasedFromGallery(packageRegistration.Id, packageRegistration.DownloadCount, downloadCount);
                }

                packageRegistration.DownloadCount = downloadCount;
            }
        }
    }
}
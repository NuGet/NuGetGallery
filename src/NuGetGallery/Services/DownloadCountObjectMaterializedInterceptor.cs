// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class DownloadCountObjectMaterializedInterceptor
        : IObjectMaterializedInterceptor
    {
        private readonly IDownloadCountService _downloadCountService;

        public DownloadCountObjectMaterializedInterceptor(IDownloadCountService downloadCountService)
        {
            _downloadCountService = downloadCountService;
        }

        public void InterceptObjectMaterialized(object entity)
        {
            InterceptPackageMaterialized(entity as Package);
            InterceptPackageRegistrationMaterialized(entity as PackageRegistration);
        }

        protected void InterceptPackageMaterialized(Package package)
        {
            if (package == null)
            {
                return;
            }

            int downloadCount;
            if (_downloadCountService.TryGetDownloadCountForPackage(package.PackageRegistration.Id, package.Version, out downloadCount))
            {
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
                packageRegistration.DownloadCount = downloadCount;
            }
        }
    }
}
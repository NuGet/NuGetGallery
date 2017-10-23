// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class OwnerRequestsListItemViewModel
    {
        public OwnerRequestsListItemViewModel(PackageOwnerRequest request, IPackageService packageService)
        {
            Request = request;
            Package = packageService.FindPackageByIdAndVersion(request.PackageRegistration.Id, version: null, semVerLevelKey: SemVerLevelKey.SemVer2, allowPrerelease: true);
        }

        public PackageOwnerRequest Request { get; }

        public Package Package { get; }
    }
}
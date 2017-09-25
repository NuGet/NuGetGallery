// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class OwnerRequestsListItemViewModel
    {
        public PackageOwnerRequest Request { get; }

        public Package Package { get; }
        
        public OwnerRequestsListItemViewModel(PackageOwnerRequest request, IPackageService packageService)
        {
            Request = request;
            Package = packageService.FindPackageByIdAndVersion(request.PackageRegistration.Id, version: null, semVerLevelKey: SemVerLevelKey.SemVer2, allowPrerelease: true);
        }
    }
}
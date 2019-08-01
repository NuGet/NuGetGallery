// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class OwnerRequestsListItemViewModel
    {
        public OwnerRequestsListItemViewModel(PackageOwnerRequest request, IPackageService packageService, User currentUser)
        {
            Request = request;

            var package = packageService.FindPackageByIdAndVersion(request.PackageRegistration.Id, version: null, semVerLevelKey: SemVerLevelKey.SemVer2, allowPrerelease: true);
            Package = new ListPackageItemViewModelFactory().Create(package, currentUser);

            CanAccept = ActionsRequiringPermissions.HandlePackageOwnershipRequest.CheckPermissions(currentUser, Request.NewOwner) == PermissionsCheckResult.Allowed;
            CanCancel = Package.CanManageOwners;
        }

        public PackageOwnerRequest Request { get; }

        public ListPackageItemViewModel Package { get; }

        public bool CanAccept { get; }

        public bool CanCancel { get; }
    }
}
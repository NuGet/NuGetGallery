// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ManagePackagesSerializableOwnerRequestViewModel
    {
        public ManagePackagesSerializableOwnerRequestViewModel(
            OwnerRequestsListItemViewModel request,
            RouteUrlTemplate<IPackageVersionModel> packageUrlTemplate,
            RouteUrlTemplate<OwnerRequestsListItemViewModel> confirmUrlTemplate,
            RouteUrlTemplate<OwnerRequestsListItemViewModel> rejectUrlTemplate,
            RouteUrlTemplate<OwnerRequestsListItemViewModel> cancelUrlTemplate,
            RouteUrlTemplate<User> profileUrlTemplate)
        {
            Id = request.Request.PackageRegistration.Id;
            PackageIconUrl = PackageHelper.ShouldRenderUrl(request.Package.IconUrl) ? request.Package.IconUrl : null;
            PackageUrl = packageUrlTemplate.Resolve(request.Package);
            Owners = request.Package.Owners.Select(o => new ManagePackagesSerializableOwnerViewModel(o, profileUrlTemplate));
            Requesting = new ManagePackagesSerializableOwnerViewModel(request.Request.RequestingOwner, profileUrlTemplate);
            New = new ManagePackagesSerializableOwnerViewModel(request.Request.NewOwner, profileUrlTemplate);
            CanAccept = request.CanAccept;
            CanCancel = request.CanCancel;
            ConfirmUrl = confirmUrlTemplate.Resolve(request);
            RejectUrl = rejectUrlTemplate.Resolve(request);
            CancelUrl = cancelUrlTemplate.Resolve(request);
        }

        public string Id { get; }
        public string PackageIconUrl { get; }
        public string PackageUrl { get; }
        public IEnumerable<ManagePackagesSerializableOwnerViewModel> Owners { get; }
        public ManagePackagesSerializableOwnerViewModel Requesting { get; }
        public ManagePackagesSerializableOwnerViewModel New { get; }
        public bool CanAccept { get; }
        public bool CanCancel { get; }
        public string ConfirmUrl { get; }
        public string RejectUrl { get; }
        public string CancelUrl { get; }
    }
}
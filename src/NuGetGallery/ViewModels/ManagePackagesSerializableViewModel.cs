// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ManagePackagesSerializableViewModel
    {
        public ManagePackagesSerializableViewModel(
            ManagePackagesViewModel model,
            RouteUrlTemplate<User> profileUrlTemplate,
            RouteUrlTemplate<IPackageVersionModel> packageUrlTemplate,
            RouteUrlTemplate<string> searchUrlTemplate,
            RouteUrlTemplate<OwnerRequestsListItemViewModel> confirmUrlTemplate,
            RouteUrlTemplate<OwnerRequestsListItemViewModel> rejectUrlTemplate,
            RouteUrlTemplate<OwnerRequestsListItemViewModel> cancelUrlTemplate)
        {
            Owners = model.Owners;

            ListedPackages = model.ListedPackages;
            UnlistedPackages = model.UnlistedPackages;

            ReservedNamespaces = model.ReservedNamespaces.ReservedNamespaces.Select(
                n => new ManagePackagesSerializableReservedNamespaceViewModel(
                    n, 
                    searchUrlTemplate, 
                    profileUrlTemplate));

            RequestsReceived = model.OwnerRequests.Received.Requests.Select(
                r => new ManagePackagesSerializableOwnerRequestViewModel(
                    r, 
                    packageUrlTemplate, 
                    confirmUrlTemplate, 
                    rejectUrlTemplate, 
                    cancelUrlTemplate, 
                    profileUrlTemplate));

            RequestsSent = model.OwnerRequests.Sent.Requests.Select(
                r => new ManagePackagesSerializableOwnerRequestViewModel(
                    r,
                    packageUrlTemplate,
                    confirmUrlTemplate,
                    rejectUrlTemplate,
                    cancelUrlTemplate,
                    profileUrlTemplate));
        }
        
        public IEnumerable<string> Owners { get; }
        public dynamic ListedPackages { get; set; }
        public dynamic UnlistedPackages { get; set; }
        public IEnumerable<ManagePackagesSerializableReservedNamespaceViewModel> ReservedNamespaces { get; }
        public IEnumerable<ManagePackagesSerializableOwnerRequestViewModel> RequestsReceived { get; }
        public IEnumerable<ManagePackagesSerializableOwnerRequestViewModel> RequestsSent { get; }
    }
}
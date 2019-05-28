// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGetGallery.Services.PackageManagement;

namespace NuGetGallery
{
    public class OwnerRequestsViewModel
    {
        /// <summary>
        /// A model to show the requests for this user to become an owner of a package.
        /// </summary>
        public OwnerRequestsListViewModel Received { get; }

        /// <summary>
        /// A model to show the requests this user has sent to other users to become owners.
        /// </summary>
        public OwnerRequestsListViewModel Sent { get; }

        public OwnerRequestsViewModel(
            IEnumerable<PackageOwnerRequest> received,
            IEnumerable<PackageOwnerRequest> sent,
            User currentUser,
            IPackageService packageService)
        {
            Received = new OwnerRequestsListViewModel(received, currentUser, packageService);
            Sent = new OwnerRequestsListViewModel(sent, currentUser, packageService);
        }
    }
}
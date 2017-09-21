// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class OwnerRequestsViewModel
    {
        /// <summary>
        /// A model to show the requests for this user to become an owner of a package.
        /// </summary>
        public OwnerRequestsListViewModel Incoming { get; }

        /// <summary>
        /// A model to show the requests this user has sent to other users to become owners.
        /// </summary>
        public OwnerRequestsListViewModel Outgoing { get; }

        public OwnerRequestsViewModel(IEnumerable<PackageOwnerRequest> incoming, IEnumerable<PackageOwnerRequest> outgoing, User currentUser, IPackageService packageService)
        {
            Incoming = new OwnerRequestsListViewModel(incoming, nameof(Incoming), currentUser, packageService);
            Outgoing = new OwnerRequestsListViewModel(outgoing, nameof(Outgoing), currentUser, packageService);
        }
    }
}
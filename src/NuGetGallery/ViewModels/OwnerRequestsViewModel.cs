// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class OwnerRequestsViewModel
    {
        public OwnerRequestsListViewModel Incoming { get; }

        public OwnerRequestsListViewModel Outgoing { get; }

        public OwnerRequestsViewModel(IEnumerable<PackageOwnerRequest> incoming, IEnumerable<PackageOwnerRequest> outgoing, User currentUser, IPackageService packageService)
        {
            Incoming = new OwnerRequestsListViewModel(incoming, nameof(Incoming), currentUser, packageService);
            Outgoing = new OwnerRequestsListViewModel(outgoing, nameof(Outgoing), currentUser, packageService);
        }
    }
}
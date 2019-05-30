// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class OwnerRequestsListViewModel
    {
        public OwnerRequestsListViewModel(IEnumerable<PackageOwnerRequest> requests, User currentUser, IPackageService packageService)
        {
            Requests = requests.Select(r => new OwnerRequestsListItemViewModel(r, packageService, currentUser)).ToArray();
        }

        public IEnumerable<OwnerRequestsListItemViewModel> Requests { get; }
    }
}
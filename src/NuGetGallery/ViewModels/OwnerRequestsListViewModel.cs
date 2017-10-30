// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class OwnerRequestsListViewModel
    {
        public OwnerRequestsListViewModel(IEnumerable<PackageOwnerRequest> requests, string name, User currentUser, IPackageService packageService)
        {
            RequestItems = requests.Select(r => new OwnerRequestsListItemViewModel(r, packageService)).ToArray();
            Name = name;
            CurrentUser = currentUser;
        }

        public IEnumerable<OwnerRequestsListItemViewModel> RequestItems { get; }

        public string Name { get; }

        public User CurrentUser { get; }
    }
}
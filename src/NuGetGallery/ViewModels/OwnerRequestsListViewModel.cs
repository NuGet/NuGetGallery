// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class OwnerRequestsListViewModel
    {
        public IEnumerable<OwnerRequestsListItemViewModel> RequestItems { get; private set; }

        public string Name { get; private set; }

        public User CurrentUser { get; private set; }
        
        public OwnerRequestsListViewModel(IEnumerable<PackageOwnerRequest> requests, string name, User currentUser, IPackageService packageService)
        {
            RequestItems = requests.Select(r => new OwnerRequestsListItemViewModel(r, packageService)).ToArray();
            Name = name;
            CurrentUser = currentUser;
        }
    }
}
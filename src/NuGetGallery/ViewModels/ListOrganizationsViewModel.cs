// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class ListOrganizationsViewModel
    {
        public IEnumerable<ListOrganizationsItemViewModel> Organizations { get; }

        public ListOrganizationsViewModel(User currentUser, IPackageService packageService)
        {
            Organizations = currentUser.Organizations.Select(m => new ListOrganizationsItemViewModel(m, packageService));
        }
    }
}
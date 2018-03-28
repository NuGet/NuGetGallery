// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class DeleteAccountPackagesListViewModel
    {
        public bool IsOrganization { get; set; }

        public IEnumerable<ListPackageItemViewModel> Packages { get; set; }

        public DeleteAccountPackagesListViewModel(User user, IEnumerable<ListPackageItemViewModel> packages)
        {
            IsOrganization = user is Organization;
            Packages = packages;
        }
    }
}
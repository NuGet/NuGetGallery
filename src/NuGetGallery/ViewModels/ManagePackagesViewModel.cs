// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class ManagePackagesViewModel
    {
        public IEnumerable<ListPackageOwnerViewModel> Owners { get; set; }

        public IEnumerable<ListPackageItemRequiredSignerViewModel> ListedPackages { get; set; }

        public IEnumerable<ListPackageItemRequiredSignerViewModel> UnlistedPackages { get; set; }

        public OwnerRequestsViewModel OwnerRequests { get; set; }

        public ReservedNamespaceListViewModel ReservedNamespaces { get; set; }
    }
}
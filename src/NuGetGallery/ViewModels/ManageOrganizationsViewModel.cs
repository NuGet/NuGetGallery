// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ManageOrganizationsViewModel
    {
        public IEnumerable<ManageOrganizationsItemViewModel> Organizations { get; }

        public ManageOrganizationsViewModel(User currentUser, IPackageService packageService)
        {
            var organizations = currentUser.Organizations.Select(m => new ManageOrganizationsItemViewModel(m, packageService));
            var pendingMemberships = currentUser.OrganizationRequests.Select(m => new ManageOrganizationsItemViewModel(m, packageService));
            var pendingTransformations = currentUser.OrganizationMigrationRequests.Select(m => new ManageOrganizationsItemViewModel(m, packageService));

            Organizations = organizations.Concat(pendingMemberships).Concat(pendingTransformations);
        }
    }
}
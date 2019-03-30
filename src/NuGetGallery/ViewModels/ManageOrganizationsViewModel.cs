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

        public ManageOrganizationsViewModel(User currentUser)
        {
            var organizations = currentUser.Organizations.Select(m => new ManageOrganizationsItemViewModel(m));
            var pendingMemberships = currentUser.OrganizationRequests.Select(m => new ManageOrganizationsItemViewModel(m));
            var pendingTransformations = currentUser.OrganizationMigrationRequests.Select(m => new ManageOrganizationsItemViewModel(m));

            Organizations = organizations.Concat(pendingMemberships).Concat(pendingTransformations);
        }
    }
}
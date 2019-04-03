// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class DeleteOrganizationViewModel : DeleteAccountViewModel<Organization>
    {
        public DeleteOrganizationViewModel(
            Organization organizationToDelete, 
            User currentUser, 
            IPackageService packageService)
            : base(organizationToDelete, currentUser, packageService)
        {
            Members = organizationToDelete.Members
                .Select(m => new OrganizationMemberViewModel(m))
                .ToList();

            AdditionalMembers = organizationToDelete.Members
                .Where(m => !m.Member.MatchesUser(currentUser))
                .Select(m => new OrganizationMemberViewModel(m))
                .ToList();
        }

        public IEnumerable<OrganizationMemberViewModel> Members { get; set; }
        
        public IEnumerable<OrganizationMemberViewModel> AdditionalMembers { get; set; }

        public bool HasAdditionalMembers => AdditionalMembers.Any();
    }
}
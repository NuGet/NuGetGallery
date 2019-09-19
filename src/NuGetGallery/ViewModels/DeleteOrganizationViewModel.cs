// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class DeleteOrganizationViewModel : DeleteAccountViewModel
    {
        public DeleteOrganizationViewModel(
            Organization organizationToDelete, 
            IReadOnlyCollection<DeleteAccountListPackageItemViewModel> ownedPackages,
            IReadOnlyList<OrganizationMemberViewModel> members,
            IReadOnlyList<OrganizationMemberViewModel> additionalMembers)
            : base(organizationToDelete, ownedPackages)
        {
            Members = members ?? throw new ArgumentNullException(nameof(members));
            AdditionalMembers = additionalMembers ?? throw new ArgumentNullException(nameof(additionalMembers));
        }

        public IReadOnlyList<OrganizationMemberViewModel> Members { get; set; }
        
        public IReadOnlyList<OrganizationMemberViewModel> AdditionalMembers { get; set; }

        public bool HasAdditionalMembers => AdditionalMembers.Any();
    }
}
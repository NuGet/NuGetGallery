// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class OrganizationAccountViewModel : AccountViewModel<Organization>
    {
        public IEnumerable<OrganizationMemberViewModel> Members { get; set; }

        public bool RequiresTenant { get; set; }

        public bool CanManageMemberships { get; set; }
    }
}
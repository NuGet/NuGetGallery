// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class OrganizationAccountViewModel : AccountViewModel
    {
        public IEnumerable<OrganizationMemberViewModel> Members { get; set; }
    }
}
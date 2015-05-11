// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public class ManagePackageOwnersViewModel : ListPackageItemViewModel
    {
        public ManagePackageOwnersViewModel(Package package, IPrincipal currentUser)
            : base(package)
        {
            CurrentOwnerUsername = currentUser.Identity.Name;
            OtherOwners = Owners.Where(o => o.Username != CurrentOwnerUsername);
        }

        public bool HasOtherOwners
        {
            get { return OtherOwners.Any(); }
        }

        public string CurrentOwnerUsername { get; private set; }
        public IEnumerable<User> OtherOwners { get; private set; }
    }
}
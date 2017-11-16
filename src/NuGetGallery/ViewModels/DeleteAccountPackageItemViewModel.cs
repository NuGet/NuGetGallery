// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery
{
    public class DeleteAccountPackageItemViewModel : ListPackageItemViewModel
    {
        public bool WillBeOrphan { get; }

        public DeleteAccountPackageItemViewModel(Package package)
            : base(package)
        {
            var organization = Owners.FirstOrDefault() as Organization;

            WillBeOrphan = Owners.Count == 1 &&
                // last owner is not an organization, or is an organization with one member.
                (organization == null || organization.Members.Count == 1);
        }
    }
}
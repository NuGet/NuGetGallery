// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery
{
    public class ManageOrganizationsItemViewModel
    {
        public string Username { get; }
        public string EmailAddress { get; }
        public bool CurrentUserIsAdmin { get; }
        public int MemberCount { get; }
        public int PackagesCount { get; }

        public ManageOrganizationsItemViewModel(Membership membership, IPackageService packageService)
        {
            var organization = membership.Organization;
            Username = organization.Username;
            EmailAddress = organization.EmailAddress;
            CurrentUserIsAdmin = membership.IsAdmin;
            MemberCount = organization.Members.Count();
            PackagesCount = packageService.FindPackageRegistrationsByOwner(membership.Organization).Count();
        }
    }
}
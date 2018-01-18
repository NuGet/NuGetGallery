// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery
{
    public class ListOrganizationsItemViewModel
    {
        public string Username { get; }
        public string EmailAddress { get; }
        public bool IsAdmin { get; }
        public int MemberCount { get; }
        public int PackagesCount { get; }

        public ListOrganizationsItemViewModel(Membership membership, IPackageService packageService)
        {
            var organization = membership.Organization;
            Username = organization.Username;
            EmailAddress = organization.EmailAddress;
            IsAdmin = membership.IsAdmin;
            MemberCount = organization.Members.Count();
            PackagesCount = packageService.FindPackageRegistrationsByOwner(membership.Organization).Count();
        }
    }
}
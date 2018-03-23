// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery
{
    public class ManageOrganizationsItemViewModel
    {
        public string OrganizationName { get; }
        public string EmailAddress { get; }
        public bool CurrentUserIsAdmin { get; }
        public int MemberCount { get; }
        public int PackagesCount { get; }
        public bool IsPendingRequest { get; }
        public bool IsPendingTransformRequest { get; }
        public string ConfirmationToken { get; }

        public ManageOrganizationsItemViewModel(User user, bool isAdmin, IPackageService packageService)
        {
            OrganizationName = user.Username;
            EmailAddress = user.EmailAddress;
            CurrentUserIsAdmin = isAdmin;
            PackagesCount = packageService.FindPackageRegistrationsByOwner(user).Count();
            MemberCount = 0;
        }

        public ManageOrganizationsItemViewModel(Organization organization, bool isAdmin, IPackageService packageService)
            : this(organization as User, isAdmin, packageService)
        {
            OrganizationName = organization.Username;
            EmailAddress = organization.EmailAddress ?? organization.UnconfirmedEmailAddress;
            CurrentUserIsAdmin = isAdmin;
            MemberCount = organization.Members.Count();
        }

        public ManageOrganizationsItemViewModel(Membership membership, IPackageService packageService)
            : this(membership.Organization, membership.IsAdmin, packageService)
        {
        }

        public ManageOrganizationsItemViewModel(MembershipRequest request, IPackageService packageService)
            : this(request.Organization, request.IsAdmin, packageService)
        {
            IsPendingRequest = true;
            ConfirmationToken = request.ConfirmationToken;
        }

        public ManageOrganizationsItemViewModel(OrganizationMigrationRequest request, IPackageService packageService)
            : this(request.NewOrganization, true, packageService)
        {
            IsPendingRequest = true;
            IsPendingTransformRequest = true;
            ConfirmationToken = request.ConfirmationToken;
        }
    }
}
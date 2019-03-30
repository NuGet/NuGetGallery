// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ManageOrganizationsItemViewModel
    {
        /// <summary>
        /// The name of this organization.
        /// </summary>
        public string OrganizationName { get; }

        /// <summary>
        /// The email address of the organization.
        /// </summary>
        public string EmailAddress { get; }

        /// <summary>
        /// Whether or not the current user is an administrator or pending administrator of this organization.
        /// </summary>
        public bool CurrentUserIsAdmin { get; }

        /// <summary>
        /// The number of members in this organization.
        /// </summary>
        public int MemberCount { get; }

        /// <summary>
        /// The number of packages owned by this organization.
        /// </summary>
        public int PackagesCount { get; }

        /// <summary>
        /// Whether or not this is a pending request--the current user is not yet a member of this organization, but can choose to accept or decline a request to become one.
        /// </summary>
        public bool IsPendingRequest { get; }

        /// <summary>
        /// Whether or not this is a request for a user account to be transformed into an organization with the current user as the administrator.
        /// </summary>
        public bool IsPendingTransformRequest { get; }

        /// <summary>
        /// If this is a pending request, the confirmation token to confirm or decline the request.
        /// </summary>
        public string ConfirmationToken { get; }

        private ManageOrganizationsItemViewModel(User user, bool isAdmin)
        {
            OrganizationName = user.Username;
            EmailAddress = user.EmailAddress;
            CurrentUserIsAdmin = isAdmin;
            PackagesCount = user.PackageRegistrations.Count();
            MemberCount = 0;
        }

        public ManageOrganizationsItemViewModel(Organization organization, bool isAdmin)
            : this(organization as User, isAdmin)
        {
            OrganizationName = organization.Username;
            EmailAddress = organization.EmailAddress ?? organization.UnconfirmedEmailAddress;
            CurrentUserIsAdmin = isAdmin;
            MemberCount = organization.Members.Count();
        }

        public ManageOrganizationsItemViewModel(Membership membership)
            : this(membership.Organization, membership.IsAdmin)
        {
        }

        public ManageOrganizationsItemViewModel(MembershipRequest request)
            : this(request.Organization, request.IsAdmin)
        {
            IsPendingRequest = true;
            ConfirmationToken = request.ConfirmationToken;
        }

        public ManageOrganizationsItemViewModel(OrganizationMigrationRequest request)
            : this(request.NewOrganization, true)
        {
            IsPendingRequest = true;
            IsPendingTransformRequest = true;
            ConfirmationToken = request.ConfirmationToken;
        }
    }
}
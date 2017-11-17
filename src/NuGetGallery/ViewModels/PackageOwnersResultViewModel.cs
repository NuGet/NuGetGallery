// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class PackageOwnersResultViewModel
    {
        public string Name;

        public string EmailAddress;

        public string ProfileUrl;

        public string ImageUrl;

        public bool GrantsCurrentUserAccess;

        public bool IsCurrentUserMemberOfOrganization;

        public bool Pending;

        public bool IsNamespaceOwner;

        public PackageOwnersResultViewModel(User user, User currentUser, UrlHelper url, bool isPending, bool isNamespaceOwner)
        {
            Name = user.Username;
            EmailAddress = user.EmailAddress;
            ProfileUrl = url.User(user, relativeUrl: false);
            ImageUrl = GravatarHelper.Url(user.EmailAddress, size: Constants.GravatarImageSize);
            GrantsCurrentUserAccess = PermissionsService.IsActionAllowed(user, currentUser, AccountActions.ManagePackageOwnershipOnBehalfOf);
            IsCurrentUserMemberOfOrganization = PermissionsService.IsActionAllowed(user, currentUser, AccountActions.DisplayPrivateOrganization);
            Pending = isPending;
            IsNamespaceOwner = isNamespaceOwner;
        }
    }
}
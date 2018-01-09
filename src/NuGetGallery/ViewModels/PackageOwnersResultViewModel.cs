﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;
using NuGetGallery.Helpers;
using System.Linq;

namespace NuGetGallery
{
    public class PackageOwnersResultViewModel
    {
        public string Name;

        public string EmailAddress;

        public string ProfileUrl;

        public string ImageUrl;

        public bool GrantsCurrentUserAccess;

        public bool IsCurrentUserAdminOfOrganization;

        public bool Pending;

        public bool IsNamespaceOwner;

        public PackageOwnersResultViewModel(User user, User currentUser, PackageRegistration packageRegistration, UrlHelper url, bool isPending, bool isNamespaceOwner)
        {
            Name = user.Username;
            EmailAddress = user.EmailAddress;
            ProfileUrl = url.User(user, relativeUrl: false);
            ImageUrl = GravatarHelper.Url(user.EmailAddress, size: Constants.GravatarImageSize);
            GrantsCurrentUserAccess = ActionsRequiringPermissions.ManagePackageOwnership.CheckPermissions(currentUser, user, packageRegistration) == PermissionsCheckResult.Allowed;
            IsCurrentUserAdminOfOrganization = user is Organization && (user as Organization).Members.Where(m => m.IsAdmin).Any();
            Pending = isPending;
            IsNamespaceOwner = isNamespaceOwner;
        }
    }
}
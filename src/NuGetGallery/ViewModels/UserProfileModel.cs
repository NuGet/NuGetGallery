// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class UserProfileModel
    {
        public UserProfileModel(User user, User currentUser, List<ListPackageItemViewModel> allPackages, int pageIndex, int pageSize, UrlHelper url)
        {
            User = user;
            Username = user.Username;
            EmailAddress = user.EmailAddress;
            UnconfirmedEmailAddress = user.UnconfirmedEmailAddress;
            TotalPackages = allPackages.Count;

            TotalPackageDownloadCount = allPackages.Sum(p => ((long)p.TotalDownloadCount));

            var packagePageTotalCount = (TotalPackages + pageSize - 1) / pageSize;

            Pager = new PreviousNextPagerViewModel(pageIndex, packagePageTotalCount,
                page => url.User(user, page));
            PagedPackages = allPackages.Skip(pageSize * pageIndex)
                                       .Take(pageSize).ToList();

            CanManageAccount = ActionsRequiringPermissions.ManageAccount.CheckPermissions(currentUser, user) == PermissionsCheckResult.Allowed;
            CanViewAccount = ActionsRequiringPermissions.ViewAccount.CheckPermissions(currentUser, user) == PermissionsCheckResult.Allowed;
        }

        public User User { get; private set; }
        public string Username { get; private set; }
        public string EmailAddress { get; private set; }
        public string UnconfirmedEmailAddress { get; set; }
        public ICollection<ListPackageItemViewModel> PagedPackages { get; private set; }
        public long TotalPackageDownloadCount { get; private set; }
        public int TotalPackages { get; private set; }
        public IPreviousNextPager Pager { get; private set; }
        public bool CanManageAccount { get; private set; }
        public bool CanViewAccount { get; private set; }

        public bool UserIsOrganization
        {
            get
            {
                return User is Organization;
            }
        }
    }
}
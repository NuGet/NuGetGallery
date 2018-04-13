// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery
{
    public class DeleteAccountViewModel
    {
        private Lazy<bool> _hasOrphanPackages;
        
        public DeleteAccountViewModel(User userToDelete, User currentUser, IPackageService packageService, ISupportRequestService supportRequestService)
        {
            _hasOrphanPackages = new Lazy<bool>(() => Packages.Any(p => p.HasSingleOwner));

            User = userToDelete;

            Packages = packageService
                 .FindPackagesByAnyMatchingOwner(User, includeUnlisted: true)
                 .Select(p => new ListPackageItemViewModel(p, currentUser))
                 .ToList();

            HasPendingRequests = supportRequestService.GetIssues()
                .Where(issue => 
                    (issue.UserKey.HasValue && issue.UserKey.Value == User.Key) &&
                    string.Equals(issue.IssueTitle, Strings.AccountDelete_SupportRequestTitle) &&
                    issue.Key != IssueStatusKeys.Resolved).Any();

            Organizations = User.Organizations.Select(m => new ManageOrganizationsItemViewModel(m, packageService));
        }

        public List<ListPackageItemViewModel> Packages { get; }

        public IEnumerable<ManageOrganizationsItemViewModel> Organizations { get; }

        public User User { get; }

        public string AccountName => User.Username;

        public bool HasPendingRequests { get; }

        public bool HasOrphanPackages
        {
            get
            {
                return Packages == null ? false : _hasOrphanPackages.Value;
            }
        }
    }
}
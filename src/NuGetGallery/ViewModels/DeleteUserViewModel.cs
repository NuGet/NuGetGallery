// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Services.PackageManagement;
using NuGetGallery.Services.SupportRequest;

namespace NuGetGallery
{
    public class DeleteUserViewModel : DeleteAccountViewModel<User>
    {
        public DeleteUserViewModel(
            User userToDelete,
            User currentUser,
            IPackageService packageService,
            ISupportRequestService supportRequestService)
            : base(userToDelete, currentUser, packageService)
        {
            Organizations = userToDelete.Organizations
                .Select(u => new ManageOrganizationsItemViewModel(u, packageService));

            HasPendingRequests = supportRequestService.GetIssues()
                .Where(issue => 
                    (issue.UserKey.HasValue && issue.UserKey.Value == userToDelete.Key) &&
                    string.Equals(issue.IssueTitle, Strings.AccountDelete_SupportRequestTitle) &&
                    issue.Key != IssueStatusKeys.Resolved).Any();
        }

        public IEnumerable<ManageOrganizationsItemViewModel> Organizations { get; }

        public bool HasPendingRequests { get; }
    }
}
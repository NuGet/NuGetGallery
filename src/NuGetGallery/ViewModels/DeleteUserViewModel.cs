// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using System.Linq;

namespace NuGetGallery
{
    public class DeleteUserViewModel : DeleteAccountViewModel<User>
    {
        public DeleteUserViewModel(
            User userToDelete,
            User currentUser,
            IPackageService packageService,
            ISupportRequestService supportRequestService)
            : base(userToDelete, currentUser, packageService, p => p.HasSingleUserOwner)
        {
            HasPendingRequests = supportRequestService.GetIssues()
                .Where(issue => 
                    (issue.UserKey.HasValue && issue.UserKey.Value == userToDelete.Key) &&
                    string.Equals(issue.IssueTitle, Strings.AccountDelete_SupportRequestTitle) &&
                    issue.Key != IssueStatusKeys.Resolved).Any();
        }

        public bool HasPendingRequests { get; }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class DeleteAccountSearchResult
    {
        public string AccountName { get; }
        public bool IsDeleted { get; }
        public string ProfileLink { get; }
        public string DeleteLink { get; }
        public string RenameLink { get; }

        public DeleteAccountSearchResult(
            string accountName, 
            bool isDeleted,
            string profileLink,
            string deleteLink,
            string renameLink)
        {
            AccountName = accountName;
            IsDeleted = isDeleted;
            ProfileLink = profileLink;
            DeleteLink = deleteLink;
            RenameLink = renameLink;
        }
    }
}